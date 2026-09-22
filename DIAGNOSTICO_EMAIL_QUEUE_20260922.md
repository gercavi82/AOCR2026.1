# 🔴 Email Queue Schema Issue – Production Diagnostics

**Fecha:** 2026-09-22  
**Severidad:** MEDIUM (Afecta notificaciones por email, NO afecta Punto 4)  
**Relacionado a Punto 4:** ❌ NO

---

## Resumen del Problema

El servicio de cola de correos electrónicos en producción está fallando porque:

**Error de Base de Datos:**
```
ERROR: column "estado" of relation "email_queue" does not exist
SQL state: 42703
```

**Ubicación:**
- Archivo: `CapaDatos/Services/EmailQueueService.cs`
- Método: `ActualizarEstadoAsync()` (línea 539)
- Tabla: `email_queue`

---

## Causa Raíz

### El Problema:
La tabla `email_queue` fue creada incompleta (sin columnas requeridas):
- ❌ Falta columna `status` (o está mal nombrada como `estado`)
- ❌ Falta columna `error_message`
- ❌ Faltan otras columnas de seguimiento

### Por Qué Ocurre:
El script de creación inicial de tabla (`create_email_queue_table.sql` o `email_pdf_tables.sql`):
1. Define tabla con pocos campos
2. Pero el código (`EmailQueueService.cs`) espera más columnas
3. Mismatch entre schema y código

---

## Impacto

| Componente | Impactado | Detalles |
|-----------|----------|---------|
| **Punto 4 (Carga Múltiple)** | ❌ NO | Los archivos se cargan normalmente |
| **Solicitudes AOCR** | ❌ NO | Las solicitudes se crean correctamente |
| **Notificaciones Email** | ⚠️ SÍ | Queue procesa pero falla al guardar estado |
| **Despliegue de Punto 4** | ❌ NO | No bloquea despliegue |

**Severidad:** Email fallará ocasionalmente, pero sistema sigue funcionando.

---

## Solución

He creado un script de corrección:

**Archivo:** `scripts/fix_email_queue_schema_20260922.sql`

**Lo que hace:**
1. Crea tabla `email_queue` si no existe
2. Agrega columnas faltantes (`status`, `error_message`, etc.)
3. Crea índices para performance
4. Verifica que todo está correcto

### Ejecutar el Fix:

**Opción A: Desde PowerShell (Recomendado)**
```powershell
cd C:\proyectos\AOCR

# Ejecutar el script SQL en la BD
$connectionString = "Host=172.20.16.90;Port=5432;Username=postgres;Password=XXX;Database=dgac_des"
psql -c "SELECT version();" $connectionString

# Luego ejecutar el fix
psql -f "scripts/fix_email_queue_schema_20260922.sql" $connectionString
```

**Opción B: Desde pgAdmin o cliente PostgreSQL**
1. Abrir pgAdmin → Conectar a BD: `dgac_des`
2. Ir a Query Tool
3. Copiar contenido de `fix_email_queue_schema_20260922.sql`
4. Ejecutar (F5)

**Opción C: El código lo ejecuta automáticamente**
- `EmailQueueService.cs` tiene método `EnsureEmailQueueSchema()`
- En la próxima compilación y despliegue, intentará ejecutar CREATE/ALTER automáticamente
- Pero es mejor ejecutar el fix script de una vez

---

## Validación Post-Fix

Después de ejecutar el script, verificar:

```sql
-- 1. Verificar columnas
SELECT column_name, data_type 
FROM information_schema.columns 
WHERE table_name = 'email_queue'
ORDER BY ordinal_position;

-- Debe mostrar:
-- id, to_address, subject, body, status ✓, solicitud_id, orden_id, 
-- created_at, proximo_intento, event_key, error_message ✓, intentos, etc.

-- 2. Verificar índices
SELECT indexname FROM pg_indexes WHERE tablename = 'email_queue';

-- Debe mostrar:
-- idx_email_queue_status_next ✓
-- idx_email_queue_solicitud
-- uq_email_queue_event_key
-- etc.

-- 3. Test rápido: Intentar insertar registro
INSERT INTO public.email_queue 
  (to_address, subject, body, status, created_at, proximo_intento)
VALUES 
  ('test@test.com', 'Test', 'Test Body', 'PENDIENTE', NOW(), NOW());
-- Debe completar sin errores
```

---

## Próximos Pasos

1. ✅ **Ejecutar el script de fix** → `fix_email_queue_schema_20260922.sql`
2. ✅ **Validar con queries arriba**
3. ✅ **Reiniciar aplicación web** en producción
4. ✅ **Verificar logs** - No deben aparecer errores de email_queue
5. ✅ **Test manual** - Crear solicitud y verificar que email se encola

---

## Relación con Punto 4

**IMPORTANTE:** Este problema NO está relacionado con Punto 4 (Carga Múltiple de Archivos):
- Punto 4 solo afecta vistas de creación/subsanación
- Punto 4 no toca `EmailQueueService.cs`
- Punto 4 no toca tabla `email_queue`
- Error ocurre en procesamiento de email asincrónico, no en upload de archivos

**Conclusión:** Puedes desplegar Punto 4 sin resolver esto. Pero sí debes resolver esto para que el email funcione correctamente.

---

## Documentación

- Script: [fix_email_queue_schema_20260922.sql](fix_email_queue_schema_20260922.sql)
- Código: [CapaDatos/Services/EmailQueueService.cs](../CapaDatos/Services/EmailQueueService.cs#L377)
- Error Log: [Logs AOCR_20260922](../)
