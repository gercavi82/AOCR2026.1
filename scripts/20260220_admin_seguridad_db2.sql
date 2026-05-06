-- ================================================================
-- AOCR - Modulo Administracion de Usuarios / Roles / Permisos
-- IBM DB2 / DB2 for i - Script idempotente (referencial)
-- Fecha: 2026-02-20
-- Nota:
--   Ajuste el terminador de sentencias segun su cliente DB2
--   (por ejemplo: SET TERMINATOR @)
-- ================================================================

BEGIN
    -- ------------------------------------------------
    -- 1) Extensiones de tabla USUARIO
    -- ------------------------------------------------
    IF NOT EXISTS (
        SELECT 1
        FROM SYSIBM.SYSCOLUMNS
        WHERE TBNAME = 'USUARIO'
          AND NAME = 'MUST_CHANGE_PASSWORD'
    ) THEN
        ALTER TABLE USUARIO
            ADD COLUMN MUST_CHANGE_PASSWORD SMALLINT NOT NULL DEFAULT 0;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM SYSIBM.SYSCOLUMNS
        WHERE TBNAME = 'USUARIO'
          AND NAME = 'PASSWORD_CHANGED_AT'
    ) THEN
        ALTER TABLE USUARIO
            ADD COLUMN PASSWORD_CHANGED_AT TIMESTAMP;
    END IF;
END;

BEGIN
    -- ------------------------------------------------
    -- 2) Tabla SEGURIDAD_PERMISO
    -- ------------------------------------------------
    IF NOT EXISTS (
        SELECT 1
        FROM SYSIBM.SYSTABLES
        WHERE NAME = 'SEGURIDAD_PERMISO'
    ) THEN
        CREATE TABLE SEGURIDAD_PERMISO
        (
            ID_PERMISO      BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            CODIGO          VARCHAR(80)  NOT NULL,
            NOMBRE          VARCHAR(180) NOT NULL,
            MODULO          VARCHAR(80)  NOT NULL,
            ACTIVO          SMALLINT     NOT NULL DEFAULT 1,
            CREADO_EN       TIMESTAMP    NOT NULL DEFAULT CURRENT TIMESTAMP,
            CREADO_POR      VARCHAR(100),
            ACTUALIZADO_EN  TIMESTAMP,
            ACTUALIZADO_POR VARCHAR(100)
        );
    END IF;
END;

BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM SYSIBM.SYSINDEXES
        WHERE NAME = 'UQ_SEG_PERM_CODIGO'
    ) THEN
        CREATE UNIQUE INDEX UQ_SEG_PERM_CODIGO
            ON SEGURIDAD_PERMISO (CODIGO);
    END IF;
END;

BEGIN
    -- ------------------------------------------------
    -- 3) Tabla SEGURIDAD_ROL_PERMISO
    -- ------------------------------------------------
    IF NOT EXISTS (
        SELECT 1
        FROM SYSIBM.SYSTABLES
        WHERE NAME = 'SEGURIDAD_ROL_PERMISO'
    ) THEN
        CREATE TABLE SEGURIDAD_ROL_PERMISO
        (
            CODIGOROL       INTEGER      NOT NULL,
            ID_PERMISO      BIGINT       NOT NULL,
            ACTIVO          SMALLINT     NOT NULL DEFAULT 1,
            CREADO_EN       TIMESTAMP    NOT NULL DEFAULT CURRENT TIMESTAMP,
            CREADO_POR      VARCHAR(100),
            ACTUALIZADO_EN  TIMESTAMP,
            ACTUALIZADO_POR VARCHAR(100),
            CONSTRAINT PK_SEGURIDAD_ROL_PERMISO PRIMARY KEY (CODIGOROL, ID_PERMISO)
        );
    END IF;
END;

BEGIN
    -- ------------------------------------------------
    -- 4) Tabla AUDITORIA_SEGURIDAD
    -- ------------------------------------------------
    IF NOT EXISTS (
        SELECT 1
        FROM SYSIBM.SYSTABLES
        WHERE NAME = 'AUDITORIA_SEGURIDAD'
    ) THEN
        CREATE TABLE AUDITORIA_SEGURIDAD
        (
            ID_AUDITORIA         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            ACTOR_USUARIO_ID     INTEGER,
            ACTOR_CODIGO_USUARIO VARCHAR(64),
            ACCION               VARCHAR(60) NOT NULL,
            OBJETIVO_TIPO        VARCHAR(40) NOT NULL,
            OBJETIVO_ID          VARCHAR(80),
            DETALLE_JSON         CLOB(64K),
            FECHA                TIMESTAMP NOT NULL DEFAULT CURRENT TIMESTAMP,
            IP                   VARCHAR(64)
        );
    END IF;
END;

-- ------------------------------------------------
-- 5) Seeds de roles base (si faltan)
-- ------------------------------------------------
BEGIN
    IF EXISTS (
        SELECT 1
        FROM SYSIBM.SYSCOLUMNS
        WHERE TBNAME = 'ROL'
          AND NAME = 'ACTIVO'
    ) THEN
        MERGE INTO ROL AS T
        USING (
            VALUES
                ('ADMINISTRADOR'),
                ('DIRECCION'),
                ('JEFATURATECNICA'),
                ('FINANCIERO'),
                ('COORDINACIONLEGAL'),
                ('COORDINADORFINANCIERO'),
                ('COORDINADORLEGAL'),
                ('OPERADOR'),
                ('SOLICITANTE'),
                ('INSPECTOR'),
                ('TECNICO')
        ) AS S(DESCRIPCION)
        ON UCASE(TRIM(T.DESCRIPCION)) = UCASE(TRIM(S.DESCRIPCION))
        WHEN NOT MATCHED THEN
            INSERT (DESCRIPCION, ACTIVO)
            VALUES (S.DESCRIPCION, 1);
    ELSE
        MERGE INTO ROL AS T
        USING (
            VALUES
                ('ADMINISTRADOR'),
                ('DIRECCION'),
                ('JEFATURATECNICA'),
                ('FINANCIERO'),
                ('COORDINACIONLEGAL'),
                ('COORDINADORFINANCIERO'),
                ('COORDINADORLEGAL'),
                ('OPERADOR'),
                ('SOLICITANTE'),
                ('INSPECTOR'),
                ('TECNICO')
        ) AS S(DESCRIPCION)
        ON UCASE(TRIM(T.DESCRIPCION)) = UCASE(TRIM(S.DESCRIPCION))
        WHEN NOT MATCHED THEN
            INSERT (DESCRIPCION)
            VALUES (S.DESCRIPCION);
    END IF;
END;

-- ------------------------------------------------
-- 6) Seeds de permisos (MERGE idempotente)
-- ------------------------------------------------
MERGE INTO SEGURIDAD_PERMISO AS T
USING (
    VALUES
        ('ADM_GESTION_USUARIOS',      'Gestionar usuarios',                  'ADMINISTRACION'),
        ('ADM_ROLES_PERMISOS',        'Gestionar roles y permisos',          'ADMINISTRACION'),
        ('ADM_RESET_PASSWORD',        'Resetear contrasena de usuarios',     'ADMINISTRACION'),
        ('FIN_VER_PAGOS',             'Verificar pagos',                     'FINANCIERO'),
        ('FIN_APROBAR_PAGO',          'Aprobar pago y registrar factura',    'FINANCIERO'),
        ('ORD_ANULAR',                'Anular orden de recaudacion',         'ORDENES'),
        ('LEGAL_REVISAR_SOLICITUD',   'Revisar solicitudes en coordinacion', 'LEGAL'),
        ('LEGAL_GENERAR_CERTIFICADO', 'Generar certificados',                'LEGAL')
) AS S(CODIGO, NOMBRE, MODULO)
ON T.CODIGO = S.CODIGO
WHEN MATCHED THEN
    UPDATE SET
        T.NOMBRE = S.NOMBRE,
        T.MODULO = S.MODULO,
        T.ACTIVO = 1,
        T.ACTUALIZADO_EN = CURRENT TIMESTAMP,
        T.ACTUALIZADO_POR = 'SYSTEM'
WHEN NOT MATCHED THEN
    INSERT (CODIGO, NOMBRE, MODULO, ACTIVO, CREADO_EN, CREADO_POR)
    VALUES (S.CODIGO, S.NOMBRE, S.MODULO, 1, CURRENT TIMESTAMP, 'SYSTEM');

-- ------------------------------------------------
-- 7) Seeds rol-permiso basicos
-- ------------------------------------------------
-- Nota: Esta seccion asume que la tabla ROL contiene DESCRIPCION y CODIGOROL.
--       Si su catalogo usa otros nombres, ajuste el SELECT.

MERGE INTO SEGURIDAD_ROL_PERMISO AS T
USING (
    SELECT R.CODIGOROL, P.ID_PERMISO
    FROM ROL R
    JOIN SEGURIDAD_PERMISO P
      ON P.CODIGO IN ('ADM_GESTION_USUARIOS', 'ADM_ROLES_PERMISOS', 'ADM_RESET_PASSWORD')
    WHERE UCASE(TRIM(R.DESCRIPCION)) IN ('ADMINISTRADOR', 'DIRECCION', 'JEFATURATECNICA')
) AS S(CODIGOROL, ID_PERMISO)
ON T.CODIGOROL = S.CODIGOROL AND T.ID_PERMISO = S.ID_PERMISO
WHEN MATCHED THEN
    UPDATE SET
        T.ACTIVO = 1,
        T.ACTUALIZADO_EN = CURRENT TIMESTAMP,
        T.ACTUALIZADO_POR = 'SYSTEM'
WHEN NOT MATCHED THEN
    INSERT (CODIGOROL, ID_PERMISO, ACTIVO, CREADO_EN, CREADO_POR)
    VALUES (S.CODIGOROL, S.ID_PERMISO, 1, CURRENT TIMESTAMP, 'SYSTEM');

MERGE INTO SEGURIDAD_ROL_PERMISO AS T
USING (
    SELECT R.CODIGOROL, P.ID_PERMISO
    FROM ROL R
    JOIN SEGURIDAD_PERMISO P
      ON P.CODIGO IN ('FIN_VER_PAGOS', 'FIN_APROBAR_PAGO')
    WHERE UCASE(TRIM(R.DESCRIPCION)) IN ('FINANCIERO', 'COORDINADORFINANCIERO', 'DIRECTORFINANCIERO', 'ADMINISTRADOR')
) AS S(CODIGOROL, ID_PERMISO)
ON T.CODIGOROL = S.CODIGOROL AND T.ID_PERMISO = S.ID_PERMISO
WHEN MATCHED THEN
    UPDATE SET
        T.ACTIVO = 1,
        T.ACTUALIZADO_EN = CURRENT TIMESTAMP,
        T.ACTUALIZADO_POR = 'SYSTEM'
WHEN NOT MATCHED THEN
    INSERT (CODIGOROL, ID_PERMISO, ACTIVO, CREADO_EN, CREADO_POR)
    VALUES (S.CODIGOROL, S.ID_PERMISO, 1, CURRENT TIMESTAMP, 'SYSTEM');

MERGE INTO SEGURIDAD_ROL_PERMISO AS T
USING (
    SELECT R.CODIGOROL, P.ID_PERMISO
    FROM ROL R
    JOIN SEGURIDAD_PERMISO P
      ON P.CODIGO IN ('LEGAL_REVISAR_SOLICITUD', 'LEGAL_GENERAR_CERTIFICADO')
    WHERE UCASE(TRIM(R.DESCRIPCION)) IN ('COORDINACIONLEGAL', 'COORDINADORLEGAL', 'DIRECTORGENERAL', 'ADMINISTRADOR')
) AS S(CODIGOROL, ID_PERMISO)
ON T.CODIGOROL = S.CODIGOROL AND T.ID_PERMISO = S.ID_PERMISO
WHEN MATCHED THEN
    UPDATE SET
        T.ACTIVO = 1,
        T.ACTUALIZADO_EN = CURRENT TIMESTAMP,
        T.ACTUALIZADO_POR = 'SYSTEM'
WHEN NOT MATCHED THEN
    INSERT (CODIGOROL, ID_PERMISO, ACTIVO, CREADO_EN, CREADO_POR)
    VALUES (S.CODIGOROL, S.ID_PERMISO, 1, CURRENT TIMESTAMP, 'SYSTEM');
