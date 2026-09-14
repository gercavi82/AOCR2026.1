-- =====================================================================
-- SCRIPT MAESTRO CONSOLIDADO: MIGRACIONES AC-01 a AC-10 (pgAdmin / DBeaver / psql)
-- BASE DE DATOS: PostgreSQL (AOCR STAGING / PRODUCCIÓN)
-- FECHA: 2026-09-07 (Actualizado para soporte directo en pgAdmin)
-- DESCRIPCIÓN: 
-- Script 100% SQL puro y autocontenido que concatena e integra en una 
-- sola transacción atómica todas las migraciones de AC-01 a AC-10.
-- Compatible con pgAdmin Query Tool, DBeaver, Navicat y psql (sin comandos \i).
-- =====================================================================

BEGIN TRANSACTION;

-- =====================================================================
-- 1. AC-01: LIBERACIÓN DEL CORREO RT DEVUELTO
-- =====================================================================
DO $$
BEGIN
    -- 1.1 Columna para resguardar el correo original del postulante
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'usuario' 
          AND column_name = 'correo_original'
    ) THEN
        ALTER TABLE public.usuario ADD COLUMN correo_original VARCHAR(255);
        COMMENT ON COLUMN public.usuario.correo_original IS 'Resguardo del correo original cuando una postulación RT es devuelta';
    END IF;

    -- 1.2 Columna booleana que indica si la reserva del correo fue liberada
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'usuario' 
          AND column_name = 'correo_liberado'
    ) THEN
        ALTER TABLE public.usuario ADD COLUMN correo_liberado BOOLEAN NOT NULL DEFAULT FALSE;
        COMMENT ON COLUMN public.usuario.correo_liberado IS 'Indica si el correo fue liberado por devolución de la designación provisional';
    END IF;

    -- 1.3 Fecha en la que Coordinación devolvió la designación
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'usuario' 
          AND column_name = 'fecha_devolucion_designacion'
    ) THEN
        ALTER TABLE public.usuario ADD COLUMN fecha_devolucion_designacion TIMESTAMP;
        COMMENT ON COLUMN public.usuario.fecha_devolucion_designacion IS 'Fecha/hora en la que la designación fue devuelta por Coordinación';
    END IF;

    -- 1.4 ID del usuario Coordinador que ejecutó la devolución
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'usuario' 
          AND column_name = 'coordinador_devolucion_id'
    ) THEN
        ALTER TABLE public.usuario ADD COLUMN coordinador_devolucion_id INTEGER;
        COMMENT ON COLUMN public.usuario.coordinador_devolucion_id IS 'ID del usuario Coordinador que devolvió la postulación';
    END IF;

    -- 1.5 Observación registrada para la devolución
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'usuario' 
          AND column_name = 'observacion_devolucion'
    ) THEN
        ALTER TABLE public.usuario ADD COLUMN observacion_devolucion TEXT;
        COMMENT ON COLUMN public.usuario.observacion_devolucion IS 'Observación y justificación técnica de la devolución ingresada por Coordinación';
    END IF;

    -- 1.6 Índice condicional para optimizar búsqueda de correos activos / no liberados
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE schemaname = 'public' 
          AND tablename = 'usuario' 
          AND indexname = 'idx_usuario_correo_activo_lower'
    ) THEN
        CREATE INDEX idx_usuario_correo_activo_lower 
        ON public.usuario (LOWER(correo)) 
        WHERE (correo_liberado = FALSE);
    END IF;
END $$;


-- =====================================================================
-- 2. AC-02: FECHAS DE INSPECCIÓN POR ESTACIÓN INDEPENDIENTES
-- =====================================================================
DO $$
BEGIN
    -- 2.1 Crear tabla aditiva de estaciones por solicitud
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables 
        WHERE table_schema = 'public' 
          AND table_name = 'aocr_tbsolicitud_estacion'
    ) THEN
        CREATE TABLE public.aocr_tbsolicitud_estacion (
            id SERIAL PRIMARY KEY,
            solicitud_id INTEGER NOT NULL,
            estacion_codigo VARCHAR(20) NOT NULL,
            estacion_nombre VARCHAR(150) NOT NULL,
            fecha_inicio DATE NOT NULL,
            fecha_fin DATE NOT NULL,
            inspector_id INTEGER NULL,
            inspector_nombre VARCHAR(200) NULL,
            inspeccion_id INTEGER NULL,
            estado VARCHAR(50) NOT NULL DEFAULT 'SOLICITADA',
            version INTEGER NOT NULL DEFAULT 1,
            activo BOOLEAN NOT NULL DEFAULT TRUE,
            observacion TEXT NULL,
            creado_en TIMESTAMP NOT NULL DEFAULT NOW(),
            creado_por INTEGER NULL,
            actualizado_en TIMESTAMP NULL,
            actualizado_por INTEGER NULL,
            CONSTRAINT chk_fechas_estacion CHECK (fecha_fin >= fecha_inicio)
        );

        COMMENT ON TABLE public.aocr_tbsolicitud_estacion IS 'Estaciones operativas solicitadas y rangos de fechas de inspección independientes (AC-02)';
        COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.solicitud_id IS 'Identificador de la solicitud AOCR (aocr_tbsolicitud.codigo_solicitud)';
        COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.estacion_codigo IS 'Código OACI/IATA o identificador de la estación (ej. UIO, GYE, MEC, LTX)';
        COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.estacion_nombre IS 'Nombre descriptivo de la estación/aeropuerto';
        COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.fecha_inicio IS 'Fecha inicial programada o solicitada para la inspección de la estación';
        COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.fecha_fin IS 'Fecha final programada o solicitada para la inspección de la estación';
        COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.inspector_id IS 'ID del inspector asignado específicamente a la estación (si aplica)';
        COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.inspeccion_id IS 'ID de la inspección vinculada en aocr_tbinspeccion (si aplica)';
        COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.estado IS 'Estado de la estación en el flujo (SOLICITADA, PLANIFICADA, INSPECCIONADA, etc.)';
    END IF;

    -- 2.2 Índice por solicitud_id
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE schemaname = 'public' 
          AND tablename = 'aocr_tbsolicitud_estacion' 
          AND indexname = 'idx_solicitud_estacion_solicitud'
    ) THEN
        CREATE INDEX idx_solicitud_estacion_solicitud 
        ON public.aocr_tbsolicitud_estacion (solicitud_id) 
        WHERE (activo = TRUE);
    END IF;

    -- 2.3 Índice único de estación activa por solicitud
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE schemaname = 'public' 
          AND tablename = 'aocr_tbsolicitud_estacion' 
          AND indexname = 'idx_solicitud_estacion_unicidad'
    ) THEN
        CREATE UNIQUE INDEX idx_solicitud_estacion_unicidad 
        ON public.aocr_tbsolicitud_estacion (solicitud_id, UPPER(estacion_codigo)) 
        WHERE (activo = TRUE);
    END IF;
END $$;


-- =====================================================================
-- 3. AC-05: DESIGNACIÓN DEL INSPECTOR POR DIRCAV
-- =====================================================================
CREATE TABLE IF NOT EXISTS public.aocr_tbdesignacion_inspector (
    id SERIAL PRIMARY KEY,
    solicitud_id INTEGER NOT NULL REFERENCES public.aocr_tbsolicitud(codigo_solicitud),
    inspeccion_id INTEGER NULL,
    estacion_id INTEGER NULL REFERENCES public.aocr_tbsolicitud_estacion(id),
    inspector_id INTEGER NOT NULL,
    inspector_cedula VARCHAR(30) NOT NULL,
    inspector_nombre VARCHAR(200) NOT NULL,
    inspector_apoyo_cedula VARCHAR(30) NULL,
    inspector_apoyo_nombre VARCHAR(200) NULL,
    dircav_usuario_id INTEGER NOT NULL,
    dircav_usuario_nombre VARCHAR(200) NULL,
    estado VARCHAR(80) NOT NULL DEFAULT 'DESIGNACION_PENDIENTE_FIRMA_DIRCAV',
    motivo TEXT NULL,
    version INTEGER NOT NULL DEFAULT 1,
    vigente BOOLEAN NOT NULL DEFAULT TRUE,
    fecha_designacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    fecha_firma TIMESTAMP WITHOUT TIME ZONE NULL,
    creado_en TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    creado_por VARCHAR(100) NULL,
    actualizado_en TIMESTAMP WITHOUT TIME ZONE NULL,
    actualizado_por VARCHAR(100) NULL
);

COMMENT ON TABLE public.aocr_tbdesignacion_inspector IS 
    'Registro formal e histórico de designaciones y reasignaciones de inspectores por DIRCAV (AC-05).';

CREATE UNIQUE INDEX IF NOT EXISTS uq_aocr_designacion_vigente 
    ON public.aocr_tbdesignacion_inspector (solicitud_id, COALESCE(estacion_id, 0)) 
    WHERE vigente = TRUE;

CREATE INDEX IF NOT EXISTS idx_aocr_designacion_solicitud 
    ON public.aocr_tbdesignacion_inspector (solicitud_id);

CREATE INDEX IF NOT EXISTS idx_aocr_designacion_inspector 
    ON public.aocr_tbdesignacion_inspector (inspector_cedula);

CREATE INDEX IF NOT EXISTS idx_aocr_designacion_estado 
    ON public.aocr_tbdesignacion_inspector (estado);


-- =====================================================================
-- 4. AC-06: PDF OFICIAL DE DESIGNACIÓN Y FIRMA DE DIRCAV
-- =====================================================================
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'aocr_tbdesignacion_inspector') THEN
        
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tbdesignacion_inspector' AND column_name = 'ruta_pdf') THEN
            ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN ruta_pdf VARCHAR(500) NULL;
        END IF;

        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tbdesignacion_inspector' AND column_name = 'ruta_documento_firmado') THEN
            ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN ruta_documento_firmado VARCHAR(500) NULL;
        END IF;

        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tbdesignacion_inspector' AND column_name = 'hash_documento') THEN
            ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN hash_documento VARCHAR(256) NULL;
        END IF;

        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tbdesignacion_inspector' AND column_name = 'firmado') THEN
            ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN firmado BOOLEAN NOT NULL DEFAULT FALSE;
        END IF;

        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tbdesignacion_inspector' AND column_name = 'usuario_firma') THEN
            ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN usuario_firma VARCHAR(200) NULL;
        END IF;

        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tbdesignacion_inspector' AND column_name = 'tamanio_bytes') THEN
            ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN tamanio_bytes BIGINT NULL;
        END IF;

        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tbdesignacion_inspector' AND column_name = 'mime_type') THEN
            ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN mime_type VARCHAR(100) NOT NULL DEFAULT 'application/pdf';
        END IF;

    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_aocr_designacion_firmado
    ON public.aocr_tbdesignacion_inspector (solicitud_id, firmado, vigente);


-- =====================================================================
-- 5. AC-07: LISTA DE VERIFICACIÓN INDEPENDIENTE POR ESTACIÓN
-- =====================================================================
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'aocr_tblv_operacional_eae') THEN
        
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tblv_operacional_eae' AND column_name = 'solicitud_id') THEN
            ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN solicitud_id INTEGER NULL;
        END IF;

        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tblv_operacional_eae' AND column_name = 'estacion_id') THEN
            ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN estacion_id INTEGER NULL;
        END IF;

        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tblv_operacional_eae' AND column_name = 'tipo_lista') THEN
            ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN tipo_lista VARCHAR(50) NOT NULL DEFAULT 'EAE';
        END IF;

        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tblv_operacional_eae' AND column_name = 'vigente') THEN
            ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN vigente BOOLEAN NOT NULL DEFAULT TRUE;
        END IF;

    END IF;
END $$;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tblv_operacional_eae' AND column_name = 'solicitud_id')
       AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'aocr_tbinspeccion') THEN
        
        UPDATE public.aocr_tblv_operacional_eae lv
           SET solicitud_id = i.codigo_solicitud
          FROM public.aocr_tbinspeccion i
         WHERE lv.codigo_inspeccion = i.codigo_inspeccion
           AND (lv.solicitud_id IS NULL OR lv.solicitud_id = 0);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_aocr_tblv_eae_solicitud_estacion
    ON public.aocr_tblv_operacional_eae(solicitud_id, estacion_id, codigo_inspeccion, version DESC);

CREATE INDEX IF NOT EXISTS ix_aocr_tblv_eae_vigente_lookup
    ON public.aocr_tblv_operacional_eae(solicitud_id, COALESCE(estacion_id, 0), tipo_lista)
    WHERE vigente = TRUE;

DROP INDEX IF EXISTS uq_aocr_tblv_eae_vigente;
CREATE UNIQUE INDEX uq_aocr_tblv_eae_vigente
    ON public.aocr_tblv_operacional_eae(solicitud_id, COALESCE(estacion_id, 0), tipo_lista)
    WHERE vigente = TRUE;


-- =====================================================================
-- 6. AC-10: CONDICIONES Y LIMITACIONES (GENERACIÓN Y FIRMA DIRCAV)
-- =====================================================================
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'aocr_tbcondiciones_limitaciones') THEN
        CREATE TABLE public.aocr_tbcondiciones_limitaciones (
            id SERIAL PRIMARY KEY,
            codigo_solicitud INTEGER NOT NULL,
            codigo_inspeccion INTEGER NULL,
            codigo_informe INTEGER NULL,
            numero_aocr VARCHAR(100) NULL,
            version INTEGER NOT NULL DEFAULT 1,
            estado VARCHAR(50) NOT NULL DEFAULT 'CL_BORRADOR',
            vigente BOOLEAN NOT NULL DEFAULT TRUE,
            
            compania VARCHAR(250) NULL,
            operador_extranjero VARCHAR(250) NULL,
            representante_tecnico VARCHAR(250) NULL,
            tipo_operacion VARCHAR(100) NULL,
            rutas_autorizadas TEXT NULL,
            alcance_autorizado TEXT NULL,
            condiciones_aprobadas TEXT NULL,
            limitaciones TEXT NULL,
            observaciones TEXT NULL,
            
            inspector_usuario_id INTEGER NULL,
            inspector_nombre VARCHAR(200) NULL,
            fecha_generacion TIMESTAMP NOT NULL DEFAULT NOW(),
            
            coordinador_usuario_id INTEGER NULL,
            coordinador_nombre VARCHAR(200) NULL,
            observacion_coordinador TEXT NULL,
            fecha_revision_coordinador TIMESTAMP NULL,
            
            dircav_usuario_id INTEGER NULL,
            dircav_nombre VARCHAR(200) NULL,
            observacion_dircav TEXT NULL,
            fecha_firma_dircav TIMESTAMP NULL,
            
            ruta_pdf_borrador VARCHAR(500) NULL,
            ruta_pdf_firmado VARCHAR(500) NULL,
            hash_pdf VARCHAR(128) NULL,
            hash_pdf_firmado VARCHAR(128) NULL,
            tamanio_pdf BIGINT NULL,
            codigo_verificacion VARCHAR(64) NULL,
            
            version_concurrencia BIGINT NOT NULL DEFAULT 1,
            created_at TIMESTAMP NOT NULL DEFAULT NOW(),
            updated_at TIMESTAMP NOT NULL DEFAULT NOW()
        );
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'aocr_tbcondiciones_limitaciones') THEN
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS codigo_inspeccion INTEGER NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS codigo_informe INTEGER NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS numero_aocr VARCHAR(100) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS version INTEGER NOT NULL DEFAULT 1;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS estado VARCHAR(50) NOT NULL DEFAULT 'CL_BORRADOR';
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS vigente BOOLEAN NOT NULL DEFAULT TRUE;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS compania VARCHAR(250) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS operador_extranjero VARCHAR(250) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS representante_tecnico VARCHAR(250) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS tipo_operacion VARCHAR(100) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS rutas_autorizadas TEXT NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS alcance_autorizado TEXT NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS condiciones_aprobadas TEXT NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS limitaciones TEXT NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS observaciones TEXT NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS inspector_usuario_id INTEGER NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS inspector_nombre VARCHAR(200) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS fecha_generacion TIMESTAMP NOT NULL DEFAULT NOW();
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS coordinador_usuario_id INTEGER NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS coordinador_nombre VARCHAR(200) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS observacion_coordinador TEXT NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS fecha_revision_coordinador TIMESTAMP NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS dircav_usuario_id INTEGER NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS dircav_nombre VARCHAR(200) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS observacion_dircav TEXT NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS fecha_firma_dircav TIMESTAMP NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS ruta_pdf_borrador VARCHAR(500) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS ruta_pdf_firmado VARCHAR(500) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS hash_pdf VARCHAR(128) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS hash_pdf_firmado VARCHAR(128) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS tamanio_pdf BIGINT NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS codigo_verificacion VARCHAR(64) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS version_concurrencia BIGINT NOT NULL DEFAULT 1;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS created_at TIMESTAMP NOT NULL DEFAULT NOW();
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP NOT NULL DEFAULT NOW();
    END IF;

    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_cl_estado') THEN
        ALTER TABLE public.aocr_tbcondiciones_limitaciones DROP CONSTRAINT ck_cl_estado;
    END IF;

    ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD CONSTRAINT ck_cl_estado CHECK (estado IN (
        'CL_NO_GENERADA',
        'CL_BORRADOR',
        'CL_PENDIENTE_COORDINADOR',
        'CL_DEVUELTA_INSPECTOR',
        'CL_PENDIENTE_DIRCAV',
        'CL_DEVUELTA_COORDINADOR',
        'CL_PENDIENTE_FIRMA_DIRCAV',
        'CL_FIRMADA_DIRCAV',
        'CL_ANULADA',
        'CL_REEMPLAZADA'
    ));
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_cl_solicitud_vigente
    ON public.aocr_tbcondiciones_limitaciones(codigo_solicitud)
    WHERE vigente = TRUE;

CREATE UNIQUE INDEX IF NOT EXISTS ux_cl_solicitud_version
    ON public.aocr_tbcondiciones_limitaciones(codigo_solicitud, version);

CREATE INDEX IF NOT EXISTS ix_cl_estado_vigente
    ON public.aocr_tbcondiciones_limitaciones(estado, vigente);

CREATE INDEX IF NOT EXISTS ix_cl_solicitud_fecha
    ON public.aocr_tbcondiciones_limitaciones(codigo_solicitud, fecha_generacion DESC);


-- =====================================================================
-- 7. ROLES Y SEGREGACIÓN ESTRICTA DIRCAV / DIRDAC / ADMINISTRADOR
-- =====================================================================
UPDATE public.rol
SET descripcion = 'DIRCAV',
    fechamodificado = CURRENT_TIMESTAMP,
    usuariomodificado = 'SYSTEM_MIGRACION_CANONICA'
WHERE codigorol = 27 AND (descripcion = 'DCAV' OR descripcion = 'DIRCAV');

UPDATE public.rol
SET descripcion = 'DIRDAC',
    activo = TRUE,
    fechamodificado = CURRENT_TIMESTAMP,
    usuariomodificado = 'SYSTEM_MIGRACION_CANONICA'
WHERE codigorol = 26;

INSERT INTO public.rol (codigorol, descripcion, activo, fecha_registro, usuariocreado, fechacreado)
SELECT 28, 'COORDINADOR', TRUE, CURRENT_TIMESTAMP, 'SYSTEM_MIGRACION_CANONICA', CURRENT_TIMESTAMP
WHERE NOT EXISTS (SELECT 1 FROM public.rol WHERE descripcion = 'COORDINADOR' OR codigorol = 28);

INSERT INTO public.rol (codigorol, descripcion, activo, fecha_registro, usuariocreado, fechacreado)
SELECT 29, 'RT', TRUE, CURRENT_TIMESTAMP, 'SYSTEM_MIGRACION_CANONICA', CURRENT_TIMESTAMP
WHERE NOT EXISTS (SELECT 1 FROM public.rol WHERE descripcion = 'RT' OR codigorol = 29);

INSERT INTO public.seguridad_permiso (codigo, nombre, modulo, activo, creado_en, creado_por)
VALUES 
    ('DIRCAV_VER_BANDEJA', 'Ver bandeja de trámites DIRCAV', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_REVISAR_DOCUMENTACION', 'Revisar expediente documental para aceptación', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_ACEPTAR_DOCUMENTACION', 'Aceptar formalmente documentación técnica', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_DEVOLVER_COORDINADOR', 'Devolver expediente al Coordinador con observaciones', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_DESIGNAR_INSPECTOR', 'Designar formalmente al Inspector de la solicitud', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_FIRMAR_DESIGNACION', 'Firmar digitalmente oficio de designación de Inspector', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_REVISAR_INFORME', 'Revisar Informe Técnico remitido por Coordinación', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_REVISAR_CL', 'Revisar Condiciones y Limitaciones del AOCR', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_FIRMAR_CL', 'Firmar digitalmente Condiciones y Limitaciones', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_REMITIR_DIRDAC', 'Remitir expediente y AOCR a DIRDAC para firma', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_VER_HISTORIAL', 'Consultar historial y auditoría de trámites DIRCAV', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM')
ON CONFLICT (codigo) DO UPDATE 
SET nombre = EXCLUDED.nombre, modulo = EXCLUDED.modulo, activo = TRUE;

INSERT INTO public.seguridad_permiso (codigo, nombre, modulo, activo, creado_en, creado_por)
VALUES 
    ('DIRDAC_VER_BANDEJA', 'Ver bandeja institucional de AOCR DIRDAC', 'DIRDAC', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRDAC_REVISAR_AOCR', 'Revisar AOCR y expediente aprobado por DIRCAV', 'DIRDAC', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRDAC_FIRMAR_AOCR', 'Legalizar y firmar digitalmente documento AOCR', 'DIRDAC', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRDAC_DEVOLVER_DIRCAV', 'Devolver expediente a DIRCAV con observaciones', 'DIRDAC', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRDAC_CONFIRMAR_LEGALIZACION', 'Confirmar culminación y entrega de trámite', 'DIRDAC', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRDAC_VER_HISTORIAL', 'Consultar historial y trámites concluidos DIRDAC', 'DIRDAC', TRUE, CURRENT_TIMESTAMP, 'SYSTEM')
ON CONFLICT (codigo) DO UPDATE 
SET nombre = EXCLUDED.nombre, modulo = EXCLUDED.modulo, activo = TRUE;

INSERT INTO public.seguridad_rol_permiso (codigorol, id_permiso, activo, creado_en, creado_por)
SELECT 27, p.id_permiso, TRUE, CURRENT_TIMESTAMP, 'SYSTEM'
FROM public.seguridad_permiso p
WHERE p.modulo = 'DIRCAV'
ON CONFLICT (codigorol, id_permiso) DO UPDATE SET activo = TRUE;

INSERT INTO public.seguridad_rol_permiso (codigorol, id_permiso, activo, creado_en, creado_por)
SELECT 26, p.id_permiso, TRUE, CURRENT_TIMESTAMP, 'SYSTEM'
FROM public.seguridad_permiso p
WHERE p.modulo = 'DIRDAC'
ON CONFLICT (codigorol, id_permiso) DO UPDATE SET activo = TRUE;

UPDATE public.seguridad_rol_permiso
SET activo = FALSE
WHERE (codigorol = 27 AND id_permiso IN (SELECT id_permiso FROM public.seguridad_permiso WHERE modulo = 'DIRDAC'))
   OR (codigorol = 26 AND id_permiso IN (SELECT id_permiso FROM public.seguridad_permiso WHERE modulo = 'DIRCAV'));

UPDATE public.seguridad_rol_permiso
SET activo = FALSE
WHERE codigorol = 1 
  AND id_permiso IN (
      SELECT id_permiso FROM public.seguridad_permiso 
      WHERE codigo IN ('DIRCAV_FIRMAR_DESIGNACION', 'DIRCAV_FIRMAR_CL', 'DIRDAC_FIRMAR_AOCR')
  );

COMMIT;

-- =====================================================================
-- CONSULTA DE VERIFICACIÓN POST-EJECUCIÓN
-- =====================================================================
SELECT 'MIGRACIONES APLICADAS EXITOSAMENTE' AS resultado,
       (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN ('aocr_tbsolicitud_estacion', 'aocr_tbdesignacion_inspector', 'aocr_tbcondiciones_limitaciones')) AS tablas_aocr_verificadas,
       (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'usuario' AND column_name IN ('correo_original', 'correo_liberado')) AS columnas_usuario_verificadas,
       (SELECT COUNT(*) FROM public.seguridad_permiso WHERE modulo IN ('DIRCAV', 'DIRDAC')) AS permisos_directivos_verificados;
