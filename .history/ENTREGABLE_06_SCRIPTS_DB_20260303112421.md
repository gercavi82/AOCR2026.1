# ENTREGABLE 6 — Scripts de Base de Datos

## Notas
- Estos scripts son de **verificación y mantenimiento**.
- Las correcciones aplicadas fueron en código C#/Razor (no requirieron cambios de esquema DB).
- Ejecutar contra: `172.20.16.55:5432 / dgac_des / root`

---

## Script 1: Verificación de Tablas Dependientes

Verifica que existen todas las tablas que los DAOs consultan.

```sql
-- =============================================================
-- VERIFICACION_TABLAS.sql
-- Verifica que las tablas necesarias existen en dgac_des
-- =============================================================

DO $$
DECLARE
    tablas_requeridas text[] := ARRAY[
        'usuario',
        'rol',
        'usuariorol',
        'parametro',
        'solicitudaocr',
        'inspeccion',
        'checklist',
        'informe',
        'ordenrecaudacion',
        'pago',
        'concepto',
        'tecnico',
        'log_auditoria',
        'historial_estado',
        'notificacion'
    ];
    t text;
    existe boolean;
BEGIN
    RAISE NOTICE '=== VERIFICACION DE TABLAS ===';
    RAISE NOTICE 'Base de datos: %', current_database();
    RAISE NOTICE 'Fecha: %', NOW();
    RAISE NOTICE '';

    FOREACH t IN ARRAY tablas_requeridas LOOP
        SELECT EXISTS (
            SELECT 1 FROM information_schema.tables 
            WHERE table_schema = 'public' AND table_name = t
        ) INTO existe;
        
        IF existe THEN
            RAISE NOTICE '✅ Tabla "%" existe', t;
        ELSE
            RAISE WARNING '❌ Tabla "%" NO EXISTE', t;
        END IF;
    END LOOP;
END $$;
```

---

## Script 2: Verificación de Columnas Críticas

Verifica que las columnas referenciadas por los DAOs corregidos existen.

```sql
-- =============================================================
-- VERIFICACION_COLUMNAS.sql
-- Verifica columnas críticas post-correcciones
-- =============================================================

DO $$
DECLARE
    col_check RECORD;
BEGIN
    RAISE NOTICE '=== VERIFICACION DE COLUMNAS ===';

    -- Tabla usuario (usada por UsuarioRolController.Index)
    FOR col_check IN 
        SELECT column_name 
        FROM information_schema.columns 
        WHERE table_name = 'usuario' 
        AND column_name IN ('idusuario','codigousuario','nombreusuario','apellidousuario','correo','estadoactividad')
    LOOP
        RAISE NOTICE '✅ usuario.% existe', col_check.column_name;
    END LOOP;

    -- Tabla rol (usada por RolController)
    FOR col_check IN 
        SELECT column_name 
        FROM information_schema.columns 
        WHERE table_name = 'rol' 
        AND column_name IN ('codigorol','nombre','descripcion','activo')
    LOOP
        RAISE NOTICE '✅ rol.% existe', col_check.column_name;
    END LOOP;

    -- Tabla usuariorol (usada por UsuarioRolDAO)
    FOR col_check IN 
        SELECT column_name 
        FROM information_schema.columns 
        WHERE table_name = 'usuariorol' 
        AND column_name IN ('codigousuariorol','codigousuario','codigorol','fecha_asignacion')
    LOOP
        RAISE NOTICE '✅ usuariorol.% existe', col_check.column_name;
    END LOOP;

    -- Tabla parametro (usada por ParametroController)
    FOR col_check IN 
        SELECT column_name 
        FROM information_schema.columns 
        WHERE table_name = 'parametro' 
        AND column_name IN ('codigoparametro','clave','valor','descripcion','activo')
    LOOP
        RAISE NOTICE '✅ parametro.% existe', col_check.column_name;
    END LOOP;

    -- Tabla checklist (usada por ChecklistController)
    FOR col_check IN 
        SELECT column_name 
        FROM information_schema.columns 
        WHERE table_name = 'checklist' 
        AND column_name IN ('codigochecklist','codigoinspeccion','seccion','itemnumero','descripcion','cumple','criticidad')
    LOOP
        RAISE NOTICE '✅ checklist.% existe', col_check.column_name;
    END LOOP;

    -- Tabla informe (usada por InformeController)
    FOR col_check IN 
        SELECT column_name 
        FROM information_schema.columns 
        WHERE table_name = 'informe' 
        AND column_name IN ('codigoinforme','codigoinspeccion','resumenejecutivo','conclusiones','hallazgos')
    LOOP
        RAISE NOTICE '✅ informe.% existe', col_check.column_name;
    END LOOP;
END $$;
```

---

## Script 3: Índices Recomendados

Índices para queries frecuentes de los controladores corregidos.

```sql
-- =============================================================
-- INDICES_RECOMENDADOS.sql
-- Índices para mejorar rendimiento de queries usadas
-- =============================================================

-- UsuarioRolController.Index: SELECT ... FROM usuario WHERE estadoactividad = '1'
CREATE INDEX IF NOT EXISTS idx_usuario_estadoactividad 
ON usuario (estadoactividad);

-- UsuarioRolDAO.ObtenerPorUsuario: WHERE codigousuario = @codigoUsuario
CREATE INDEX IF NOT EXISTS idx_usuariorol_codigousuario 
ON usuariorol (codigousuario);

-- UsuarioRolDAO.ObtenerPorRol: WHERE codigorol = @codigoRol
CREATE INDEX IF NOT EXISTS idx_usuariorol_codigorol 
ON usuariorol (codigorol);

-- RolController.Index: SELECT ... FROM rol ORDER BY nombre
CREATE INDEX IF NOT EXISTS idx_rol_activo_nombre 
ON rol (activo, nombre);

-- ParametroController: SELECT ... FROM parametro WHERE activo = true
CREATE INDEX IF NOT EXISTS idx_parametro_activo 
ON parametro (activo);

-- Verificar índices creados
SELECT tablename, indexname, indexdef 
FROM pg_indexes 
WHERE schemaname = 'public' 
AND indexname LIKE 'idx_%'
ORDER BY tablename, indexname;
```

---

## Script 4: Datos de Prueba Mínimos

Inserta datos mínimos para verificar que los módulos corregidos funcionan.

```sql
-- =============================================================
-- DATOS_PRUEBA.sql
-- Datos mínimos para verificación post-deploy
-- SOLO ejecutar en ambiente de desarrollo/testing
-- =============================================================

-- Rol de prueba (si no existe)
INSERT INTO rol (nombre, descripcion, activo, fechacreacion, creadopor)
SELECT 'TEST_AUDIT_ROL', 'Rol creado por auditoría de verificación', true, NOW(), 'audit'
WHERE NOT EXISTS (SELECT 1 FROM rol WHERE nombre = 'TEST_AUDIT_ROL');

-- Parámetro de prueba (si no existe)
INSERT INTO parametro (clave, valor, descripcion, activo, created_at, created_by)
SELECT 'TEST_AUDIT_PARAM', 'valor_test', 'Parámetro creado por auditoría', true, NOW(), 0
WHERE NOT EXISTS (SELECT 1 FROM parametro WHERE clave = 'TEST_AUDIT_PARAM');

-- Verificar inserciones
SELECT 'ROL' as tipo, nombre as nombre, activo::text as estado 
FROM rol WHERE nombre = 'TEST_AUDIT_ROL'
UNION ALL
SELECT 'PARAMETRO', clave, activo::text 
FROM parametro WHERE clave = 'TEST_AUDIT_PARAM';
```

---

## Script 5: Limpieza de Datos de Prueba

```sql
-- =============================================================
-- LIMPIEZA_PRUEBA.sql
-- Elimina datos de verificación de auditoría
-- =============================================================

DELETE FROM rol WHERE nombre = 'TEST_AUDIT_ROL';
DELETE FROM parametro WHERE clave = 'TEST_AUDIT_PARAM';

RAISE NOTICE 'Datos de prueba eliminados.';
```

---

## Script 6: Auditoría de Integridad Referencial

```sql
-- =============================================================
-- AUDITORIA_INTEGRIDAD.sql
-- Detecta registros huérfanos en tablas clave
-- =============================================================

-- UsuarioRol sin usuario válido
SELECT ur.codigousuariorol, ur.codigousuario, ur.codigorol
FROM usuariorol ur
LEFT JOIN usuario u ON u.idusuario = ur.codigousuario
WHERE u.idusuario IS NULL;

-- UsuarioRol sin rol válido
SELECT ur.codigousuariorol, ur.codigousuario, ur.codigorol
FROM usuariorol ur
LEFT JOIN rol r ON r.codigorol::int = ur.codigorol
WHERE r.codigorol IS NULL;

-- Inspecciones sin solicitud
SELECT i.codigoinspeccion, i.codigosolicitud
FROM inspeccion i
LEFT JOIN solicitudaocr s ON s.codigosolicitud = i.codigosolicitud
WHERE s.codigosolicitud IS NULL;

-- Checklists sin inspección
SELECT c.codigochecklist, c.codigoinspeccion
FROM checklist c
LEFT JOIN inspeccion i ON i.codigoinspeccion = c.codigoinspeccion
WHERE i.codigoinspeccion IS NULL;

-- Informes sin inspección
SELECT inf.codigoinforme, inf.codigoinspeccion
FROM informe inf
LEFT JOIN inspeccion i ON i.codigoinspeccion = inf.codigoinspeccion
WHERE i.codigoinspeccion IS NULL;
```

---

## Resumen de Scripts

| # | Script | Propósito | Riesgo |
|---|--------|-----------|--------|
| 1 | VERIFICACION_TABLAS | Confirma existencia de tablas | Ninguno (solo lectura) |
| 2 | VERIFICACION_COLUMNAS | Confirma columnas usadas por DAOs | Ninguno (solo lectura) |
| 3 | INDICES_RECOMENDADOS | Mejora rendimiento queries frecuentes | Bajo (solo agrega índices) |
| 4 | DATOS_PRUEBA | Inserta datos mínimos para testing | Bajo (inserta con guard) |
| 5 | LIMPIEZA_PRUEBA | Elimina datos de testing | Bajo (targeted DELETE) |
| 6 | AUDITORIA_INTEGRIDAD | Detecta registros huérfanos | Ninguno (solo lectura) |
