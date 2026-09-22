# SOLUCIÓN: Error "usuario_id" column does not exist en aocr_tbauditoria

## 🔴 Problema Identificado

**Error:**
```
42703: column "usuario_id" of relation "aocr_tbauditoria" does not exist
```

**Ubicación del Error:**
- [CapaDatos/DAOs/SolicitudEstacionDAO.cs](CapaDatos/DAOs/SolicitudEstacionDAO.cs#L313) - Línea 313
- [CapaDatos/DAOs/CondicionesLimitacionesDAO.cs](CapaDatos/DAOs/CondicionesLimitacionesDAO.cs#L560) - Línea 560

**Causa:**
El código intenta insertar auditoría con la columna `usuario_id`, pero esta columna no existe en la tabla `aocr_tbauditoria`.

---

## ✅ Solución: Migración de Base de Datos

Se ha creado una migración idempotente que agrega:
1. **Columna:** `usuario_id INTEGER NULL` - para rastrear qué usuario hizo cambios
2. **Índice:** `idx_aocr_tbauditoria_usuario_id` - para optimizar búsquedas

---

## 🚀 Pasos para Aplicar la Migración

### **Opción 1: pgAdmin (Recomendado - Más Seguro)**

1. Abre **pgAdmin** o **DBeaver**
2. Conéctate a: `Host: 172.20.16.55 | Port: 5432 | DB: dgac_des | User: root | Password: control`
3. Abre el editor de **Query/SQL**
4. Copia este script:

```sql
BEGIN;

ALTER TABLE IF EXISTS public.aocr_tbauditoria
ADD COLUMN IF NOT EXISTS usuario_id INTEGER NULL;

CREATE INDEX IF NOT EXISTS idx_aocr_tbauditoria_usuario_id 
ON public.aocr_tbauditoria (usuario_id);

COMMENT ON COLUMN public.aocr_tbauditoria.usuario_id IS 'ID del usuario que realizó la acción auditada (referencia a tabla usuario.idusuario)';

COMMIT;
```

5. Presiona **Ctrl+Enter** o haz clic en el botón **Play/Execute**
6. Verifica que no haya errores

---

### **Opción 2: PowerShell + psql (Si psql está instalado)**

```powershell
cd c:\proyectos\AOCR\scripts\db
.\ApplyMigration.ps1
```

Si no tienes psql, el script mostrará las instrucciones de pgAdmin automáticamente.

---

### **Opción 3: Script SQL directo**

Ejecuta el archivo SQL directamente:

```powershell
# Reemplaza con la ruta correcta a psql si la tienes
$env:PGPASSWORD='control'
psql -h 172.20.16.55 -U root -d dgac_des -f 'scripts\db\20260922_add_usuario_id_to_auditoria.sql'
```

---

## ✔️ Verificación

Después de aplicar la migración, ejecuta esta consulta para confirmar:

```sql
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_name = 'aocr_tbauditoria' 
  AND column_name = 'usuario_id';
```

**Resultado esperado:**
```
column_name  | data_type | is_nullable
-------------|-----------|------------
usuario_id   | integer   | YES
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

## 🔐 Características de Seguridad

✅ **Idempotente:** Segura de ejecutar múltiples veces
- `ADD COLUMN IF NOT EXISTS` → No falla si ya existe
- `CREATE INDEX IF NOT EXISTS` → No falla si el índice ya existe

✅ **Transaccional:** Usa `BEGIN;` y `COMMIT;` para atomicidad

✅ **Backward Compatible:** Columna es NULL (permite datos antiguos sin cambios)

---

## 📝 Próximos Pasos

**1. Aplicar la migración** (una de las 3 opciones arriba)

**2. Recompilar la solución AOCR:**
```powershell
cd c:\proyectos\AOCR
msbuild AOCR.sln /p:Configuration=Debug /p:Platform=AnyCPU
```

**3. Probar funcionalidad:**
- Crear/editar solicitud AOCR con estaciones de inspección
- Guardar condiciones y limitaciones
- Verificar que los registros se inserten sin errores en aocr_tbauditoria

**4. Verificar auditoría:**
```sql
SELECT * FROM public.aocr_tbauditoria 
WHERE usuario_id IS NOT NULL 
ORDER BY codigo_auditoria DESC 
LIMIT 5;
```

---

## 🆘 Troubleshooting

### Error: "Already exists"
✅ Normal - indica que ya se ejecutó la migración anteriormente. La migración es idempotente.

### Error: "Connection refused"
❌ Verifica que:
- BD PostgreSQL está corriendo en `172.20.16.55:5432`
- Usuario `root` existe con contraseña `control`
- Tienes acceso de red al servidor

### Error: "Permission denied"
❌ El usuario `root` no tiene permisos en el schema `public`. Contacta al DBA.

---

## 📞 Contacto & Soporte

- **Reporte de error:** Se encontró en logs: `\\172.20.16.90\aocr\Logs`
- **Fecha:** 2026-09-22 11:13:59
- **Usuario:** JFGUIJARRO
- **Módulo:** SolicitudAOCR/FormularioCompleto
