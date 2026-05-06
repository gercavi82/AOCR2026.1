-- ================================================================
-- AOCR - Modulo Administracion de Usuarios / Roles / Permisos
-- PostgreSQL - Script idempotente
-- Fecha: 2026-02-20
-- ================================================================

BEGIN;

-- ------------------------------------------------
-- 1) Extensiones / columnas de seguridad en usuario
-- ------------------------------------------------
ALTER TABLE IF EXISTS usuario
    ADD COLUMN IF NOT EXISTS must_change_password BOOLEAN DEFAULT FALSE;

ALTER TABLE IF EXISTS usuario
    ADD COLUMN IF NOT EXISTS password_changed_at TIMESTAMP NULL;

-- Asegura longitud para hashes PBKDF2 sin romper hashes anteriores.
ALTER TABLE IF EXISTS usuario
    ALTER COLUMN clave TYPE TEXT;

-- ------------------------------------------------
-- 2) Tabla de permisos
-- ------------------------------------------------
CREATE TABLE IF NOT EXISTS seguridad_permiso
(
    id_permiso      BIGSERIAL PRIMARY KEY,
    codigo          VARCHAR(80)  NOT NULL,
    nombre          VARCHAR(180) NOT NULL,
    modulo          VARCHAR(80)  NOT NULL,
    activo          BOOLEAN      NOT NULL DEFAULT TRUE,
    creado_en       TIMESTAMP    NOT NULL DEFAULT NOW(),
    creado_por      VARCHAR(100),
    actualizado_en  TIMESTAMP,
    actualizado_por VARCHAR(100)
);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'uq_seguridad_permiso_codigo'
    ) THEN
        ALTER TABLE seguridad_permiso
            ADD CONSTRAINT uq_seguridad_permiso_codigo UNIQUE (codigo);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_seguridad_permiso_modulo
    ON seguridad_permiso (modulo);

-- ------------------------------------------------
-- 3) Tabla rol-permiso
-- ------------------------------------------------
CREATE TABLE IF NOT EXISTS seguridad_rol_permiso
(
    codigorol       INTEGER      NOT NULL,
    id_permiso      BIGINT       NOT NULL,
    activo          BOOLEAN      NOT NULL DEFAULT TRUE,
    creado_en       TIMESTAMP    NOT NULL DEFAULT NOW(),
    creado_por      VARCHAR(100),
    actualizado_en  TIMESTAMP,
    actualizado_por VARCHAR(100),
    CONSTRAINT pk_seguridad_rol_permiso PRIMARY KEY (codigorol, id_permiso)
);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_seguridad_rol_permiso_rol'
    ) THEN
        ALTER TABLE seguridad_rol_permiso
            ADD CONSTRAINT fk_seguridad_rol_permiso_rol
            FOREIGN KEY (codigorol) REFERENCES rol(codigorol);
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_seguridad_rol_permiso_permiso'
    ) THEN
        ALTER TABLE seguridad_rol_permiso
            ADD CONSTRAINT fk_seguridad_rol_permiso_permiso
            FOREIGN KEY (id_permiso) REFERENCES seguridad_permiso(id_permiso);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_seguridad_rol_permiso_permiso
    ON seguridad_rol_permiso (id_permiso);

CREATE INDEX IF NOT EXISTS ix_seguridad_rol_permiso_activo
    ON seguridad_rol_permiso (activo);

-- ------------------------------------------------
-- 4) Auditoria de seguridad
-- ------------------------------------------------
CREATE TABLE IF NOT EXISTS auditoria_seguridad
(
    id_auditoria        BIGSERIAL PRIMARY KEY,
    actor_usuario_id    INTEGER NULL,
    actor_codigo_usuario VARCHAR(64),
    accion              VARCHAR(60)  NOT NULL,
    objetivo_tipo       VARCHAR(40)  NOT NULL,
    objetivo_id         VARCHAR(80),
    detalle_json        JSONB,
    fecha               TIMESTAMP    NOT NULL DEFAULT NOW(),
    ip                  VARCHAR(64)
);

CREATE INDEX IF NOT EXISTS ix_auditoria_seguridad_fecha
    ON auditoria_seguridad (fecha DESC);

CREATE INDEX IF NOT EXISTS ix_auditoria_seguridad_actor
    ON auditoria_seguridad (actor_usuario_id, fecha DESC);

CREATE INDEX IF NOT EXISTS ix_auditoria_seguridad_accion
    ON auditoria_seguridad (accion, fecha DESC);

-- ------------------------------------------------
-- 5) Seeds de roles base (si faltan)
-- ------------------------------------------------
DO $$
DECLARE
    _rol TEXT;
    _has_usuariocreado BOOLEAN;
    _has_fechacreado BOOLEAN;
BEGIN
    SELECT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'rol'
          AND column_name = 'usuariocreado'
    ) INTO _has_usuariocreado;

    SELECT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'rol'
          AND column_name = 'fechacreado'
    ) INTO _has_fechacreado;

    FOREACH _rol IN ARRAY ARRAY[
        'Administrador',
        'Direccion',
        'JefaturaTecnica',
        'Financiero',
        'CoordinacionLegal',
        'CoordinadorFinanciero',
        'CoordinadorLegal',
        'Operador',
        'Solicitante',
        'Inspector',
        'Tecnico'
    ]
    LOOP
        IF NOT EXISTS (
            SELECT 1
            FROM rol r
            WHERE UPPER(TRIM(r.descripcion)) = UPPER(TRIM(_rol))
        ) THEN
            IF _has_usuariocreado AND _has_fechacreado THEN
                EXECUTE 'INSERT INTO rol (descripcion, activo, usuariocreado, fechacreado) VALUES ($1, TRUE, ''SYSTEM'', NOW())'
                USING _rol;
            ELSIF _has_usuariocreado THEN
                EXECUTE 'INSERT INTO rol (descripcion, activo, usuariocreado) VALUES ($1, TRUE, ''SYSTEM'')'
                USING _rol;
            ELSIF _has_fechacreado THEN
                EXECUTE 'INSERT INTO rol (descripcion, activo, fechacreado) VALUES ($1, TRUE, NOW())'
                USING _rol;
            ELSE
                EXECUTE 'INSERT INTO rol (descripcion, activo) VALUES ($1, TRUE)'
                USING _rol;
            END IF;
        END IF;
    END LOOP;
END $$;

-- ------------------------------------------------
-- 6) Seeds de permisos
-- ------------------------------------------------
WITH permisos(codigo, nombre, modulo) AS
(
    VALUES
        ('ADM_GESTION_USUARIOS',       'Gestionar usuarios',                     'ADMINISTRACION'),
        ('ADM_ROLES_PERMISOS',         'Gestionar roles y permisos',             'ADMINISTRACION'),
        ('ADM_RESET_PASSWORD',         'Resetear contrasena de usuarios',        'ADMINISTRACION'),
        ('FIN_VER_PAGOS',              'Verificar pagos',                        'FINANCIERO'),
        ('FIN_APROBAR_PAGO',           'Aprobar pago y registrar factura',       'FINANCIERO'),
        ('ORD_ANULAR',                 'Anular orden de recaudacion',            'ORDENES'),
        ('LEGAL_REVISAR_SOLICITUD',    'Revisar solicitudes en coordinacion',    'LEGAL'),
        ('LEGAL_GENERAR_CERTIFICADO',  'Generar certificados',                   'LEGAL')
)
INSERT INTO seguridad_permiso (codigo, nombre, modulo, activo, creado_en, creado_por, actualizado_en, actualizado_por)
SELECT p.codigo, p.nombre, p.modulo, TRUE, NOW(), 'SYSTEM', NOW(), 'SYSTEM'
FROM permisos p
ON CONFLICT (codigo)
DO UPDATE SET
    nombre = EXCLUDED.nombre,
    modulo = EXCLUDED.modulo,
    activo = TRUE,
    actualizado_en = NOW(),
    actualizado_por = 'SYSTEM';

-- ------------------------------------------------
-- 7) Mapeo de permisos por rol
-- ------------------------------------------------
-- Administrador, Direccion y JefaturaTecnica: administracion completa.
WITH asignaciones AS
(
    SELECT r.codigorol, p.id_permiso
    FROM rol r
    JOIN seguridad_permiso p
      ON p.codigo IN ('ADM_GESTION_USUARIOS', 'ADM_ROLES_PERMISOS', 'ADM_RESET_PASSWORD')
    WHERE UPPER(TRIM(r.descripcion)) IN ('ADMINISTRADOR', 'DIRECCION', 'JEFATURATECNICA')
)
INSERT INTO seguridad_rol_permiso
    (codigorol, id_permiso, activo, creado_en, creado_por, actualizado_en, actualizado_por)
SELECT codigorol, id_permiso, TRUE, NOW(), 'SYSTEM', NOW(), 'SYSTEM'
FROM asignaciones
ON CONFLICT (codigorol, id_permiso)
DO UPDATE SET
    activo = TRUE,
    actualizado_en = NOW(),
    actualizado_por = 'SYSTEM';

-- Financiero
WITH asignaciones AS
(
    SELECT r.codigorol, p.id_permiso
    FROM rol r
    JOIN seguridad_permiso p
      ON p.codigo IN ('FIN_VER_PAGOS', 'FIN_APROBAR_PAGO')
    WHERE UPPER(TRIM(r.descripcion)) IN ('FINANCIERO', 'COORDINADORFINANCIERO', 'DIRECTORFINANCIERO', 'ADMINISTRADOR')
)
INSERT INTO seguridad_rol_permiso
    (codigorol, id_permiso, activo, creado_en, creado_por, actualizado_en, actualizado_por)
SELECT codigorol, id_permiso, TRUE, NOW(), 'SYSTEM', NOW(), 'SYSTEM'
FROM asignaciones
ON CONFLICT (codigorol, id_permiso)
DO UPDATE SET
    activo = TRUE,
    actualizado_en = NOW(),
    actualizado_por = 'SYSTEM';

-- Legal
WITH asignaciones AS
(
    SELECT r.codigorol, p.id_permiso
    FROM rol r
    JOIN seguridad_permiso p
      ON p.codigo IN ('LEGAL_REVISAR_SOLICITUD', 'LEGAL_GENERAR_CERTIFICADO')
    WHERE UPPER(TRIM(r.descripcion)) IN ('COORDINACIONLEGAL', 'COORDINADORLEGAL', 'DIRECTORGENERAL', 'ADMINISTRADOR')
)
INSERT INTO seguridad_rol_permiso
    (codigorol, id_permiso, activo, creado_en, creado_por, actualizado_en, actualizado_por)
SELECT codigorol, id_permiso, TRUE, NOW(), 'SYSTEM', NOW(), 'SYSTEM'
FROM asignaciones
ON CONFLICT (codigorol, id_permiso)
DO UPDATE SET
    activo = TRUE,
    actualizado_en = NOW(),
    actualizado_por = 'SYSTEM';

-- Anulacion de orden
WITH asignaciones AS
(
    SELECT r.codigorol, p.id_permiso
    FROM rol r
    JOIN seguridad_permiso p
      ON p.codigo = 'ORD_ANULAR'
    WHERE UPPER(TRIM(r.descripcion)) IN ('SOLICITANTE', 'OPERADOR', 'ADMINISTRADOR')
)
INSERT INTO seguridad_rol_permiso
    (codigorol, id_permiso, activo, creado_en, creado_por, actualizado_en, actualizado_por)
SELECT codigorol, id_permiso, TRUE, NOW(), 'SYSTEM', NOW(), 'SYSTEM'
FROM asignaciones
ON CONFLICT (codigorol, id_permiso)
DO UPDATE SET
    activo = TRUE,
    actualizado_en = NOW(),
    actualizado_por = 'SYSTEM';

COMMIT;

-- ------------------------------------------------
-- Consulta rapida de verificacion
-- ------------------------------------------------
-- SELECT codigo, nombre, modulo, activo FROM seguridad_permiso ORDER BY modulo, codigo;
-- SELECT r.descripcion, p.codigo
-- FROM seguridad_rol_permiso rp
-- JOIN rol r ON r.codigorol = rp.codigorol
-- JOIN seguridad_permiso p ON p.id_permiso = rp.id_permiso
-- WHERE rp.activo = TRUE
-- ORDER BY r.descripcion, p.codigo;
