# Migración: Agregar columna usuario_id a aocr_tbauditoria

**Fecha:** 2026-09-22  
**Error Resuelto:** `42703: column "usuario_id" of relation "aocr_tbauditoria" does not exist`

## Descripción del Problema

El código en `SolicitudEstacionDAO.cs` (línea 313) y `CondicionesLimitacionesDAO.cs` (línea 560) intenta insertar registros en la tabla `aocr_tbauditoria` incluyendo la columna `usuario_id`, pero esta columna no existe en la estructura actual de la tabla.

```csharp
// SolicitudEstacionDAO.cs línea 313
conn.Execute(@"
    INSERT INTO public.aocr_tbauditoria (modulo, accion, usuario_id, detalle, fecha)
    VALUES ('SOLICITUD_AOCR', 'ACTUALIZAR_ESTACIONES_INSPECCION', @usuarioId, @detalle, NOW());",
    new { usuarioId, detalle }, tx);
```

## Solución

Se agregan:
- **Columna:** `usuario_id` (INTEGER NULL) - para rastrear el usuario que realizó cambios
- **Índice:** `idx_aocr_tbauditoria_usuario_id` - para optimizar búsquedas futuras por usuario

## Cómo Ejecutar la Migración

### Opción 1: Usando pgAdmin (Recomendado)

1. Abrir pgAdmin o DBeaver
2. Conectar a la base de datos `dgac_des` (Host: 172.20.16.55, Usuario: root, Contraseña: control)
3. Abrir la herramienta de SQL/Query Editor
4. Copiar el contenido del archivo `20260922_add_usuario_id_to_auditoria.sql`
5. Ejecutar el script completo (Ctrl+Enter o botón Play)
6. Verificar que no haya errores

### Opción 2: Usando psql en Terminal

```powershell
$env:PGPASSWORD='control'
psql -h 172.20.16.55 -U root -d dgac_des -f 'scripts\db\20260922_add_usuario_id_to_auditoria.sql'
```

### Opción 3: Usando SQL Server Management Studio (SSMS) con extensión PostgreSQL

Si tienes conectores configurados, ejecuta el script desde SSMS.

## Verificación

Después de ejecutar la migración, verifica que la columna exista:

```sql
SELECT column_name, data_type 
FROM information_schema.columns 
WHERE table_name = 'aocr_tbauditoria' AND column_name = 'usuario_id';
```

Deberías ver:
```
column_name  | data_type
-------------|----------
usuario_id   | integer
```

## Archivos Modificados

- **Script de Migración:** `scripts/db/20260922_add_usuario_id_to_auditoria.sql`

## Dependencias

Esta migración es idempotente (segura de ejecutar múltiples veces):
- `ADD COLUMN IF NOT EXISTS` - no falla si ya existe
- `CREATE INDEX IF NOT EXISTS` - no falla si el índice ya existe

## Próximos Pasos

Después de aplicar la migración:

1. Recompilar la aplicación AOCR
2. Ejecutar las acciones que causaban el error:
   - Guardar formulario completo con estaciones de inspección
   - Guardar condiciones y limitaciones
3. Verificar que los registros de auditoría se inserten correctamente

## Notas de Desarrollo

- La columna `usuario_id` es opcional (NULL) para mantener compatibilidad hacia atrás
- Se recomienda en el futuro hacer que esta columna sea NOT NULL después de verificar que todos los inserts pasen el usuario_id
- El índice mejora el rendimiento de consultas futuras por usuario
