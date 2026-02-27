# GUÍA DE EJECUCIÓN COMPLETA — AS400 → PostgreSQL Mirror Sync
## Proyecto AOCR · DGAC Ecuador

---

## PREREQUISITOS

| Requisito | Verificación |
|-----------|-------------|
| PostgreSQL `dgac_des` accesible en `172.20.16.55:5432` | `psql -h 172.20.16.55 -U root -d dgac_des -c "SELECT 1"` |
| AS/400 ODBC DSN configurado en servidor IIS | ODBC Data Source Administrator → DSN AS400 |
| `AS400:Server`, `AS400:UserId`, `AS400:Password` en Web.config | Ver sección `<appSettings>` |
| `Sync:Enabled=false` (hasta que DB esté lista) | Web.config `<appSettings>` |
| App pool .NET 4.7.2 con permisos de ODBC | IIS Manager → App Pool identity |

---

## PASO 1 — Ejecutar Scripts SQL en Orden

```bash
# Desde la carpeta scripts/mirror_sync/ del repositorio
# Variables de conexión (ajustar):
HOST=172.20.16.55
PORT=5432
DB=dgac_des
USER=root

# 1. Schemas
psql -h $HOST -p $PORT -U $USER -d $DB -f 001_create_schemas.sql

# 2. Tablas de control de sync
psql -h $HOST -p $PORT -U $USER -d $DB -f 002_create_sync_tables.sql

# 3. Tablas espejo (opcar5, opcar6, usuarc, usuar1, ciaarc + columnas base)
psql -h $HOST -p $PORT -U $USER -d $DB -f 003_create_mirror_raw_tables.sql

# 4. Parche SNAP — columnas adicionales + vistas mirror_clean (IDEMPOTENTE)
psql -h $HOST -p $PORT -U $USER -d $DB -f 003b_alter_mirror_raw_add_snap_columns.sql

# 5. (OPCIONAL) Verificar estructura base
psql -h $HOST -p $PORT -U $USER -d $DB -f 004_verify_mirror_sync.sql
```

> **Los scripts 001–003 son idempotentes** (CREATE TABLE IF NOT EXISTS).  
> **003b** usa bloques `DO $$ BEGIN IF NOT EXISTS ... END $$` para columnas y `CREATE OR REPLACE VIEW`.  
> Se pueden re-ejecutar sin riesgo.

---

## PASO 2 — Configurar Web.config

Agregar o editar en `<appSettings>`:

```xml
<!-- AS400 ODBC connection details -->
<add key="AS400:Server"   value="190.152.8.185"/>
<add key="AS400:Library"  value="DGACDAT"/>
<add key="AS400:UserId"   value="DGACCONEXI"/>
<add key="AS400:Password" value="DGACTIC20@"/>

<!-- Sync flags -->
<add key="Sync:Enabled"   value="true"/>      <!-- ← cambiar a true luego de paso 1 -->
<add key="Sync:DryRun"    value="false"/>
<add key="Sync:BatchSize" value="1000"/>
<add key="Sync:CronExpression" value="0 */30 * * * *"/>  <!-- cada 30 minutos -->
```

> **NOTA SEGURIDAD**: En producción mover `AS400:Password` y la contraseña de PostgreSQL
> a `machine.config` o variables de entorno del sistema. **No commitear credenciales al repositorio.**

---

## PASO 3 — Desplegar la Aplicación

1. Compilar solución en Release → `bin/`
2. Publicar a IIS (xcopy publish o MSDeploy)
3. Reciclar Application Pool
4. Verificar que no hay errores en `App_Data/logs/` o Event Viewer

---

## PASO 4 — Primera Sincronización Manual

1. Abrir navegador → `https://[servidor]/SyncAdmin/`
2. Loguearse con usuario rol **Administrador**
3. Verificar que el panel muestra **"Sync: Habilitado"**
4. Hacer clic en **"Ejecutar Sync Completo"** (botón RunAll)
5. Esperar respuesta (pode tardar 2–10 minutos según volumen de datos AS400)
6. La página refresca automáticamente cada 30 seg (botón JS incluido)

---

## PASO 5 — Validar Datos

```bash
# Validación completa (solo SELECTs, seguro)
psql -h $HOST -p $PORT -U $USER -d $DB -f 005_validate_full_mirror_sync.sql
```

**Resultados esperados tras primer sync exitoso:**

| Sección | Resultado esperado |
|---------|-------------------|
| [1] Schemas | 3 schemas: mirror_raw, mirror_clean, sync |
| [1b] Tablas | 5 en mirror_raw, 4 en sync |
| [1c] Vistas | 4 vistas en mirror_clean |
| [2] Watermarks | status='OK' en todas las tablas habilitadas |
| [3] Conteos | total > 0 en al menos USUARC y OPCAR5 |
| [4] Duplicados | 0 filas en todos los checks |
| [5c] Huérfanos USUAR1 | 0 idealmente |
| [6] Lotes | filas con status='OK', latency_ms razonable |
| [6b] Rechazos | 0 o errores conocidos/menores |
| [7] Columnas 003b | 30 filas opcar5, 9 filas opcar6 |
| [10] Tombstones | 0 pendientes |

---

## CHECKLIST "NO ROMPE NADA"

### Rutas existentes — SIN CAMBIOS

- [x] `/FR3/RegistrarNuevo` — sin modificar
- [x] `/FR3/Consultar` — sin modificar
- [x] `/OrdenRecaudacion/*` — sin modificar
- [x] `/Home/*` — sin modificar
- [x] `/Financiero/*` — sin modificar
- [x] Todos los DAO existentes — sin modificar
- [x] `Web.config` — solo se limpiaron credenciales SMTP hardcodeadas (vacío)
- [x] Sin dependencias circulares nuevas (SyncAdminController solo usa `MirrorReadService` + `As400MirrorSyncJob`)

### Nuevas rutas (solo admin)

- [x] `/SyncAdmin/` → Index dashboard
- [x] `/SyncAdmin/Fr3` → Vista FR3 espejo
- [x] `/SyncAdmin/RunAll` (POST + AntiForgery)
- [x] `/SyncAdmin/RunTable` (POST + AntiForgery)
- [x] `/SyncAdmin/Status` (GET JSON)

Todas protegidas con `[Authorize(Roles = "Administrador")]`.

### Cambios de base de datos — Additive only

- [x] Nuevos schemas (`mirror_raw`, `mirror_clean`, `sync`) — no tocan tablas existentes
- [x] Nuevas tablas y columnas — no modifican esquema AOCR principal
- [x] `ALTER TABLE` en 003b — solo ADD COLUMN IF NOT EXISTS (nunca DROP)

---

## ROLLBACK COMPLETO

En caso de necesitar revertir completamente:

```bash
# PELIGRO: borra todos los datos del espejo.
# 1. Deshabilitar sync primero:
#    Web.config: Sync:Enabled=false → redeploy

# 2. Ejecutar rollback (descommentar sección 003b primero si es necesario)
psql -h $HOST -p $PORT -U $USER -d $DB -f 999_rollback_mirror_sync.sql
```

**El rollback NO afecta:**
- Tablas AOCR originales (dgac_des.public.*)
- Datos de producción FR3
- Usuarios/empresas/órdenes existentes

---

## MONITOREO CONTINUO

### Queries de monitoreo rápido

```sql
-- ¿Cuándo fue el último sync exitoso?
SELECT table_name, status, last_success_ts, updated_at
FROM sync.watermark ORDER BY updated_at DESC;

-- ¿Hay errores recientes?
SELECT * FROM sync.batch_log WHERE status != 'OK' ORDER BY started_at DESC LIMIT 10;

-- ¿Cuántos FR3 del año actual?
SELECT COUNT(*), SUM(gran_total) FROM mirror_clean.v_fr3_cabecera WHERE anio = EXTRACT(YEAR FROM NOW())::TEXT;
```

### Alertas recomendadas (configurar en monitoreo externo)

| Condición | Alerta |
|-----------|--------|
| `sync.watermark.status = 'ERROR'` | Crítico |
| `MAX(updated_at) < NOW() - INTERVAL '2 hours'` en watermark | Warning (sync no ha corrido) |
| `COUNT(*) > 100` en `sync.rejections` en 1 hora | Warning |
| Latencia lote > 30000ms | Informativo |

---

## ARCHIVOS MODIFICADOS/CREADOS EN ESTA IMPLEMENTACIÓN

| Archivo | Tipo | Acción |
|---------|------|--------|
| `CapaNegocio/Integraciones/As400Sync/As400MirrorSyncDefinitions.cs` | C# | Modificado — OPCAR5/OPCAR6 habilitados |
| `CapaNegocio/Integraciones/As400Sync/MirrorReadService.cs` | C# | Modificado — DTOs + métodos ListarFr3Recientes, ObtenerEstadoSync, ObtenerUltimosLotes |
| `CapaPresentacion/Controllers/SyncAdminController.cs` | C# | Creado — controller admin |
| `CapaPresentacion/Views/SyncAdmin/Index.cshtml` | Razor | Creado — dashboard sync |
| `CapaPresentacion/Views/SyncAdmin/Fr3.cshtml` | Razor | Creado — visor FR3 espejo |
| `CapaPresentacion/Web.config` | XML | Modificado — SMTP hardcoded limpiado |
| `scripts/mirror_sync/003b_alter_mirror_raw_add_snap_columns.sql` | SQL | Creado — parche SNAP + vistas |
| `scripts/mirror_sync/005_validate_full_mirror_sync.sql` | SQL | Creado — suite validación |
| `scripts/mirror_sync/999_rollback_mirror_sync.sql` | SQL | Modificado — bloque 003b añadido |

**Archivos existentes SIN CAMBIOS (solo lectura):**
- `Contracts.cs`, `As400OdbcSourceReader.cs`, `PostgresMirrorApplier.cs`
- `PostgresSyncStateStore.cs`, `As400MirrorSyncService.cs`, `As400MirrorSyncJob.cs`
- `As400MirrorSyncOptionsFactory.cs`, `001–004 SQL scripts`
- Todos los controllers/DAOs/views existentes del sitio AOCR

---

*Generado por: GitHub Copilot · Arquitecto AS400→PostgreSQL Mirror Sync · AOCR/DGAC*
