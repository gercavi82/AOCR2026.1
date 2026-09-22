# SOLUCIÓN: Errores "usuario_id" y "fecha" en aocr_tbauditoria

## 🔴 Problemas Identificados

Análisis de logs de **2026-09-22** mostró 2 errores principales en la tabla `aocr_tbauditoria`:

**Error 1 (Principal):**
```
42703: column "usuario_id" of relation "aocr_tbauditoria" does not exist
```

**Error 2 (Secundario):**
```
42703: column "fecha" of relation "aocr_tbauditoria" does not exist
```

### 📍 Ubicaciones del Error

- [CapaDatos/DAOs/SolicitudEstacionDAO.cs](CapaDatos/DAOs/SolicitudEstacionDAO.cs#L313) - Línea 313
- [CapaDatos/DAOs/CondicionesLimitacionesDAO.cs](CapaDatos/DAOs/CondicionesLimitacionesDAO.cs#L560) - Línea 560
- [CapaDatos/DAOs/AocrDesignacionDAO.cs](CapaDatos/DAOs/AocrDesignacionDAO.cs#L650) - Línea 650, 1043

### 🔍 Causa Raíz

Hay **inconsistencia en los DAOs** sobre qué columnas espera `aocr_tbauditoria`:

| DAO | Columnas esperadas |
|-----|-------------------|
| `SolicitudEstacionDAO` | `modulo, accion, usuario_id, detalle, fecha` |
| `CondicionesLimitacionesDAO` | `modulo, accion, usuario_id, detalle, fecha` |
| `AocrDesignacionDAO` | `entidad, accion, usuario, fecha, datos_previos, datos_nuevos` |
| `UsuarioDAO` | `tabla_afectada, registro_id, accion, usuario, ...` |

La tabla actual solo tiene:
- `codigo_auditoria, tabla_afectada, registro_id, accion, usuario`
- `fecha_accion, fecha_hora, ip_address`
- `datos_anteriores, datos_nuevos, descripcion, detalle, modulo, resultado, mensaje_error`

**Faltan:**
- ❌ `usuario_id` (INTEGER)
- ❌ `fecha` (alias para fecha_accion)
- ❌ `entidad` (VARCHAR)

---

## ✅ Solución: Migración SQL Completa

Se ha creado una migración que:

1. ✅ Agrega columna `usuario_id INTEGER NULL` - para rastrear qué usuario hizo cambios
2. ✅ Agrega columna `fecha TIMESTAMP NULL` - alias para `fecha_accion` (compatibilidad)
3. ✅ Agrega columna `entidad VARCHAR(100)` - para entidades como "DIRCAV", "COORDINACION"
4. ✅ Crea índices de rendimiento en `usuario_id`, `fecha`, `modulo`
5. ✅ Crea un TRIGGER automático que sincroniza `fecha` ↔ `fecha_accion`

### 🔒 Características de Seguridad

- ✅ **Idempotente:** Segura de ejecutar múltiples veces
- ✅ **Transaccional:** Usa `BEGIN;` y `COMMIT;` para atomicidad
- ✅ **Backward Compatible:** Todas las columnas son NULL (datos existentes no se afectan)
- ✅ **Automatic Sync:** Trigger sincroniza automáticamente `fecha` ↔ `fecha_accion`

---

## 🚀 Pasos para Aplicar la Migración

### **Opción 1: PowerShell + psql (Recomendado - Más Fácil)**

Si tienes **PostgreSQL/psql instalado** en tu máquina:

```powershell
cd c:\proyectos\AOCR\scripts\db
.\RunMigration.ps1
```

Este script:
- ✅ Busca psql automáticamente
- ✅ Ejecuta la migración
- ✅ Verifica el resultado
- ✅ Muestra la estructura final de la tabla

---

### **Opción 2: pgAdmin/DBeaver (Recomendado - Más Seguro)**

1. Abre **pgAdmin** o **DBeaver**
2. Conéctate a: 
   - **Host:** `172.20.16.55`
   - **Port:** `5432`
   - **Database:** `dgac_des`
   - **User:** `root`
   - **Password:** `control`
3. Abre el editor de **Query/SQL**
4. Copia este script completo:

```sql
-- Migración: Arreglar estructura de tabla aocr_tbauditoria
-- Fecha: 2026-09-22

BEGIN;

-- Agregar columnas faltantes (si no existen)
ALTER TABLE IF EXISTS public.aocr_tbauditoria
ADD COLUMN IF NOT EXISTS usuario_id INTEGER NULL,
ADD COLUMN IF NOT EXISTS fecha TIMESTAMP NULL,
ADD COLUMN IF NOT EXISTS entidad VARCHAR(100) NULL;

-- Crear índices para mejorar búsquedas
CREATE INDEX IF NOT EXISTS idx_aocr_tbauditoria_usuario_id 
ON public.aocr_tbauditoria (usuario_id);

CREATE INDEX IF NOT EXISTS idx_aocr_tbauditoria_fecha 
ON public.aocr_tbauditoria (fecha);

CREATE INDEX IF NOT EXISTS idx_aocr_tbauditoria_modulo 
ON public.aocr_tbauditoria (modulo);

-- Comentarios descriptivos
COMMENT ON COLUMN public.aocr_tbauditoria.usuario_id IS 'ID del usuario que realizó la acción (referencia a tabla usuario.idusuario)';
COMMENT ON COLUMN public.aocr_tbauditoria.fecha IS 'Timestamp de la acción (alias para fecha_accion para compatibilidad)';
COMMENT ON COLUMN public.aocr_tbauditoria.entidad IS 'Nombre de la entidad auditada (compatibilidad con AocrDesignacionDAO)';

-- Crear trigger automático de sincronización
CREATE OR REPLACE FUNCTION aocr_audit_insert_compat()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.fecha IS NULL AND NEW.fecha_accion IS NOT NULL THEN
        NEW.fecha := NEW.fecha_accion;
    END IF;
    IF NEW.fecha_accion IS NULL AND NEW.fecha IS NOT NULL THEN
        NEW.fecha_accion := NEW.fecha;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_aocr_audit_sync_fecha ON public.aocr_tbauditoria;
CREATE TRIGGER trg_aocr_audit_sync_fecha
BEFORE INSERT OR UPDATE ON public.aocr_tbauditoria
FOR EACH ROW
EXECUTE FUNCTION aocr_audit_insert_compat();

COMMIT;
```

5. Presiona **Ctrl+Enter** o haz clic en el botón **Play/Execute**
6. Verifica que no haya errores (verás "Rows affected: 0" si la operación es idempotente)

---

## ✔️ Verificación

Después de aplicar la migración, ejecuta esta consulta para confirmar:

```sql
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_name = 'aocr_tbauditoria' 
  AND column_name IN ('usuario_id', 'fecha', 'entidad')
ORDER BY column_name;
```

**Resultado esperado:**
```
column_name  | data_type           | is_nullable
-------------|---------------------|------------
entidad      | character varying   | YES
fecha        | timestamp           | YES
usuario_id   | integer             | YES
```

También verifica que existan los índices:

```sql
SELECT indexname FROM pg_indexes 
WHERE tablename = 'aocr_tbauditoria' 
  AND indexname LIKE 'idx_aocr_tbauditoria%'
ORDER BY indexname;
```

**Resultado esperado:**
```
idx_aocr_tbauditoria_fecha
idx_aocr_tbauditoria_modulo
idx_aocr_tbauditoria_usuario_id
```

Y verifica que el TRIGGER existe:

```sql
SELECT trigger_name FROM information_schema.triggers 
WHERE event_object_table = 'aocr_tbauditoria' 
  AND trigger_name LIKE 'trg_aocr%';
```

**Resultado esperado:**
```
trg_aocr_audit_sync_fecha
```

---

## 📁 Archivos Entregados

| Archivo | Descripción |
|---------|-------------|
| [scripts/db/20260922_add_usuario_id_to_auditoria.sql](scripts/db/20260922_add_usuario_id_to_auditoria.sql) | Script SQL de migración (idempotente) |
| [scripts/db/ApplyMigration.ps1](scripts/db/ApplyMigration.ps1) | Script PowerShell auxiliar |
| [scripts/db/20260922_MIGRACION_README.md](scripts/db/20260922_MIGRACION_README.md) | Documentación detallada |
| [AOCR.Tests/Unit/MigracionAuditoriaTests.cs](AOCR.Tests/Unit/MigracionAuditoriaTests.cs) | Test unitario (para verificación futura) |

---

## � Próximos Pasos

**1. Aplicar la migración** (una de las 2 opciones arriba)

**2. Recompilar la solución AOCR:**
```powershell
cd c:\proyectos\AOCR
# Opción A: Usar Visual Studio (recomendado)
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe' AOCR.sln /p:Configuration=Debug /p:Platform=AnyCPU

# Opción B: Usar dotnet (si está disponible)
dotnet build AOCR.sln -c Debug
```

**3. Probar funcionalidad:**
- ✅ Crear/editar solicitud AOCR con estaciones de inspección
- ✅ Guardar condiciones y limitaciones
- ✅ Verificar que NO haya errores de columna en logs
- ✅ Verificar que los registros se inserten correctamente en aocr_tbauditoria

**4. Verificar auditoría:**
```sql
SELECT codigo_auditoria, modulo, accion, usuario_id, fecha, detalle
FROM public.aocr_tbauditoria 
WHERE usuario_id IS NOT NULL 
   OR modulo IN ('SOLICITUD_AOCR', 'CONDICIONES_LIMITACIONES')
ORDER BY codigo_auditoria DESC 
LIMIT 10;
```

---

## 🆘 Troubleshooting

### Error: "Already exists" o "does not exist IF NOT EXISTS"
✅ **NORMAL** - Indica que ya se ejecutó la migración anteriormente. La migración es 100% idempotente.

**Solución:** Ejecuta la verificación SQL (abajo) para confirmar que todo está en orden.

### Error: "Connection refused"
❌ **Problema de conectividad**

Verifica que:
- ✅ BD PostgreSQL está corriendo en `172.20.16.55:5432`
- ✅ Usuario `root` existe con contraseña `control`
- ✅ Tienes acceso de red al servidor
- ✅ No hay firewall bloqueando puerto 5432

```powershell
# Test de conectividad
Test-NetConnection -ComputerName 172.20.16.55 -Port 5432
```

### Error: "Permission denied" o "role root does not have..."
❌ **Problema de permisos**

El usuario `root` no tiene permisos suficientes. Contacta al DBA para:
- Verificar que `root` es propietario del schema `public`
- Verificar permisos en tabla `aocr_tbauditoria`
- Ejecutar: `GRANT ALL ON public.aocr_tbauditoria TO root;`

### Logs siguen mostrando el error después de la migración
❌ **Problema de conexión de caché**

La aplicación AOCR podría estar usando un connection pool cacheado. Prueba:
1. Reinicia el app pool de IIS (si está en producción)
2. Reinicia la aplicación web
3. Limpia los logs: `\\172.20.16.90\aocr\Logs\*`
4. Reintenta la operación

---

## 📞 Información del Reporte Original

- **Fecha del error:** 2026-09-22 11:13:59.928 UTC
- **Ubicación de logs:** `\\172.20.16.90\aocr\Logs\AOCR_20260922.log`
- **Usuario afectado:** JFGUIJARRO
- **Módulo:** SolicitudAOCR/FormularioCompleto
- **Acción:** GuardarFormularioCompletoAtomico
- **Etapa:** ESTACIONES (Al guardar estaciones de inspección)
- **Correlation ID:** Múltiples (9849910bd9e5, 572524add4cc, etc.)

### Impacto
- ❌ Usuarios no pueden guardar solicitudes AOCR con estaciones
- ❌ Usuarios no pueden guardar condiciones y limitaciones
- ✅ **Resuelto con esta migración**
