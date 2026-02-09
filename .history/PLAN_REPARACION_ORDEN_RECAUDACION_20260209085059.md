# Plan de Reparación - Módulo Órdenes de Recaudación

**Fecha:** 5 de febrero de 2026  
**Prioridad:** CRÍTICA (P0)

---

## 🎯 OBJETIVO

Reparar completamente el módulo de Órdenes de Recaudación para que funcione correctamente en producción, eliminando los 5 problemas críticos identificados.

---

## 📋 DECISIÓN REQUERIDA

Antes de proceder con las reparaciones, necesitamos decidir sobre el esquema de base de datos:

### Opción 1: **AGREGAR columnas faltantes a BD** (Recomendado si se necesita esa funcionalidad)

**Pros:**
- ✅ Mantiene toda la funcionalidad del código actual
- ✅ Permite guardar información adicional (observaciones, correo, teléfono, etc.)
- ✅ No requiere cambios en modelos ni DTOs
- ✅ Compatible con la visión original del sistema

**Contras:**
- ⚠️ Requiere migración de BD
- ⚠️ Posible downtime durante migración
- ⚠️ Cambios de schema necesitan coordinación con DBA

**Script:** `scripts/fix_orden_recaudacion_sql.sql` (descomentar OPCIÓN A y B)

---

### Opción 2: **SIMPLIFICAR código a columnas existentes** (Recomendado para fix rápido)

**Pros:**
- ✅ No requiere cambios en BD
- ✅ Implementación inmediata sin downtime
- ✅ Simplifica el modelo de datos
- ✅ Menos puntos de falla

**Contras:**
- ⚠️ Se pierde funcionalidad de observaciones, correo, teléfono en orden
- ⚠️ Requiere refactorizar DAO, BL, y modelos
- ⚠️ Posibles cambios en vistas/formularios

**Archivos afectados:**
- `CapaDatos/DAOs/OrdenRecaudacionDAO.cs` (líneas 375-378, 410-423, 650-660)
- `CapaModelo/OrdenRecaudacion/OrdenRecaudacion.cs` (eliminar propiedades)
- Posibles vistas que usen esas propiedades

---

## 🚀 PLAN DE IMPLEMENTACIÓN

### FASE 1: PREPARACIÓN (30 min)

#### Paso 1.1: Backup de Base de Datos
```powershell
# Ejecutar desde servidor PostgreSQL
cd c:\AOCR\AOCR\AOCR05-01-2026\AOCR1\AOCR\scripts

# Backup completo
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFile = "backup_aocr_${timestamp}.sql"

& "C:\Program Files\PostgreSQL\16\bin\pg_dump.exe" `
    -h 172.20.16.55 `
    -U postgres `
    -d aocr_db `
    -F p `
    -f $backupFile

Write-Host "Backup creado: $backupFile" -ForegroundColor Green
```

#### Paso 1.2: Crear Branch de Trabajo
```powershell
cd c:\AOCR\AOCR\AOCR05-01-2026\AOCR1\AOCR

git checkout -b fix/orden-recaudacion-critical
git add .
git commit -m "Checkpoint antes de fixes críticos OrdenRecaudacion"
```

#### Paso 1.3: Verificar Estado Actual de BD
```powershell
# Ejecutar script de verificación
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" `
    -h 172.20.16.55 `
    -U postgres `
    -d aocr_db `
    -f scripts\fix_orden_recaudacion_sql.sql `
    -v ON_ERROR_STOP=1

# Debe mostrar las 9 columnas actuales de aocr_or_orden
# y las 7 columnas de aocr_or_orden_detalle
```

---

### FASE 2: FIXES CRÍTICOS (P0) - 2-3 horas

#### Fix 2.1: Corregir SQL en OrdenRecaudacionDAO

**Si elegiste OPCIÓN 1 (agregar columnas):**
```powershell
# 1. Ejecutar script de BD
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" `
    -h 172.20.16.55 `
    -U postgres `
    -d aocr_db `
    -c "
    ALTER TABLE aocr_or_orden
    ADD COLUMN IF NOT EXISTS observacion TEXT,
    ADD COLUMN IF NOT EXISTS subtotal NUMERIC(18,2) DEFAULT 0,
    ADD COLUMN IF NOT EXISTS admin NUMERIC(18,2) DEFAULT 0,
    ADD COLUMN IF NOT EXISTS lugar_emision VARCHAR(100),
    ADD COLUMN IF NOT EXISTS correo VARCHAR(100),
    ADD COLUMN IF NOT EXISTS telefono VARCHAR(20),
    ADD COLUMN IF NOT EXISTS concepto_id INTEGER;
    
    ALTER TABLE aocr_or_orden_detalle
    ADD COLUMN IF NOT EXISTS concepto_codigo VARCHAR(50),
    ADD COLUMN IF NOT EXISTS descripcion TEXT,
    ADD COLUMN IF NOT EXISTS porcentaje_admin NUMERIC(5,2) DEFAULT 0,
    ADD COLUMN IF NOT EXISTS subtotal NUMERIC(18,2) DEFAULT 0,
    ADD COLUMN IF NOT EXISTS admin NUMERIC(18,2) DEFAULT 0;
    "

# 2. Verificar
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" `
    -h 172.20.16.55 `
    -U postgres `
    -d aocr_db `
    -c "SELECT column_name FROM information_schema.columns WHERE table_name = 'aocr_or_orden' ORDER BY ordinal_position;"

# ✅ El DAO actual funcionará sin cambios
```

**Si elegiste OPCIÓN 2 (simplificar código):**
```powershell
# Aplicar el patch de DAO corregido (se creará en siguiente paso)
# Ver archivo: OrdenRecaudacionDAO_FIXED.patch
```

#### Fix 2.2: Corregir Patrón Async

**Reemplazar OrdenRecaudacionBL.cs:**
```powershell
# Backup del actual
Copy-Item "CapaNegocio\OrdenRecaudacionBL.cs" "CapaNegocio\OrdenRecaudacionBL.cs.backup"

# Aplicar versión corregida
Copy-Item "CapaNegocio\OrdenRecaudacionBL_FIXED.cs" "CapaNegocio\OrdenRecaudacionBL.cs"

Write-Host "OrdenRecaudacionBL.cs corregido - ahora usa async/await correctamente" -ForegroundColor Green
```

**Impacto:** El Controller necesitará cambios para usar los métodos async.

---

#### Fix 2.3: Actualizar Controller a Async

**Líneas a modificar en OrdenRecaudacionController.cs:**

1. **Método Index** (línea ~77-92):
```csharp
// ANTES
public ActionResult Index(string estado)
{
    var ordenes = _dao.ListarPorUsuarioModel(idUsuario, estado) ?? new List<OrdenRecaudacionModel>();
    return View(ordenes);
}

// DESPUÉS
public async Task<ActionResult> Index(string estado)
{
    var ordenes = await _dao.ListarPorUsuarioModelAsync(idUsuario, estado) ?? new List<OrdenRecaudacionModel>();
    return View(ordenes);
}
```

2. **Método Nueva POST** (línea ~142-170):
```csharp
// ANTES
[HttpPost]
public ActionResult Nueva(OrdenRecaudacionNuevaVM model)
{
    int ordenId = _bl.Insertar(orden);
    return RedirectToAction("Detalles", new { id = ordenId });
}

// DESPUÉS
[HttpPost]
public async Task<ActionResult> Nueva(OrdenRecaudacionNuevaVM model)
{
    int ordenId = await _bl.InsertarAsync(orden);
    return RedirectToAction("Detalles", new { id = ordenId });
}
```

**Script PowerShell para aplicar cambios:**
```powershell
# Ver archivo: fix_controller_async.ps1 (se creará en siguiente paso)
```

---

#### Fix 2.4: Resolver Constructor Dual

**Modificar OrdenRecaudacionController.cs:**

```csharp
// ANTES (líneas 28-63)
private OrdenRecaudacionDAO _ordenDAO;
private readonly OrdenRecaudacionDAO _dao = new OrdenRecaudacionDAO();  // ❌
private readonly OrdenRecaudacionBL _bl = new OrdenRecaudacionBL();     // ❌

public OrdenRecaudacionController(IOrdenRecaudacionOrchestrator orchestrator) { }
public OrdenRecaudacionController() { /*hardcoded dependencies*/ }

// DESPUÉS
private readonly IOrdenRecaudacionRepository _dao;
private readonly IOrdenRecaudacionOrchestrator _orchestrator;

// UN SOLO constructor
public OrdenRecaudacionController(
    IOrdenRecaudacionRepository dao,
    IOrdenRecaudacionOrchestrator orchestrator)
{
    _dao = dao ?? throw new ArgumentNullException(nameof(dao));
    _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
}
```

**Configurar Dependency Injection (si usas Unity):**

Editar `App_Start/UnityConfig.cs`:
```csharp
public static void RegisterComponents()
{
    var container = new UnityContainer();
    
    // Registrar repositorios
    container.RegisterType<IOrdenRecaudacionRepository, OrdenRecaudacionDAO>(
        new InjectionConstructor(
            new ResolvedParameter<string>("PostgreSQL")
        )
    );
    
    container.RegisterType<IOrdenRecaudacionOrchestrator, OrdenRecaudacionOrchestrator>();
    
    DependencyResolver.SetResolver(new UnityDependencyResolver(container));
}
```

---

### FASE 3: FIXES ALTA PRIORIDAD (P1) - 1-2 horas

#### Fix 3.1: Implementar Tarifas Configurables

```powershell
# 1. Ejecutar script de parámetros
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" `
    -h 172.20.16.55 `
    -U postgres `
    -d aocr_db `
    -f scripts\insert_parametros_tarifas.sql

# 2. Verificar inserción
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" `
    -h 172.0.16.55 `
    -U postgres `
    -d aocr_db `
    -c "SELECT clave, valor, descripcion FROM parametros WHERE clave LIKE 'TARIFA_%' ORDER BY clave;"

Write-Host "Parámetros de tarifas insertados correctamente" -ForegroundColor Green
```

**Modificar OrdenRecaudacionController.cs método AsegurarConceptosBasicos():**

Ver archivo `OrdenRecaudacionController_TARIFAS_FIXED.txt` (se creará en siguiente paso)

---

### FASE 4: TESTING - 2 horas

#### Test 4.1: Compilación
```powershell
cd c:\AOCR\AOCR\AOCR05-01-2026\AOCR1\AOCR

# Limpiar y rebuild
dotnet clean AOCR.sln
dotnet build AOCR.sln --configuration Debug

# Verificar errores
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Compilación exitosa" -ForegroundColor Green
} else {
    Write-Host "❌ Errores de compilación - revisar output" -ForegroundColor Red
}
```

#### Test 4.2: Tests Unitarios
```powershell
# Ejecutar tests
dotnet test AOCR.Tests\AOCR.Tests.csproj --verbosity normal

# Si hay tests específicos de OrdenRecaudacion:
dotnet test --filter "FullyQualifiedName~OrdenRecaudacion"
```

#### Test 4.3: Pruebas Manuales

**Checklist de pruebas:**

```
[ ] Navegar a /OrdenRecaudacion
[ ] Crear nueva orden (POST /OrdenRecaudacion/Nueva)
[ ] Verificar que se guarda correctamente en BD
[ ] Consultar SELECT * FROM aocr_or_orden ORDER BY id DESC LIMIT 1
[ ] Editar orden existente
[ ] Cambiar estado de orden
[ ] Generar PDF de orden
[ ] Registrar pago
[ ] Verificar logs de IIS (no debe haber errores 42703)
[ ] Verificar performance (< 2 segundos por request)
```

---

### FASE 5: DEPLOY - 30 min

#### Paso 5.1: Commit de Cambios
```powershell
git add .
git commit -m "Fix críticos módulo OrdenRecaudacion

- Corregido SQL con columnas existentes/agregadas
- Implementado patrón async/await correcto
- Eliminado antipatrón sync-over-async
- Resuelto constructor dual con DI
- Implementadas tarifas configurables
- Tests pasan exitosamente

Fixes aplicados:
- P0-1: SQL INSERT detalle corregido
- P0-2: SQL UPDATE orden corregido  
- P0-3: Métodos async reales implementados
- P0-4: BL sin .Result
- P0-5: DI correcta en Controller
- P1-1: Tarifas desde BD

Ver: AUDITORIA_COMPLETA_ORDEN_RECAUDACION.md"

git push origin fix/orden-recaudacion-critical
```

#### Paso 5.2: Merge a Develop
```powershell
git checkout develop
git merge fix/orden-recaudacion-critical
git push origin develop
```

#### Paso 5.3: Deploy a QA/Producción
```powershell
# Usar Azure Pipelines o proceso manual
# Verificar archivo azure-pipelines.yml
```

---

## 📊 VERIFICACIÓN POST-DEPLOY

### Checklist Final

```powershell
# 1. Verificar servicio corriendo
Invoke-WebRequest -Uri "https://aocr.dgac.gob.ec/OrdenRecaudacion" -UseBasicParsing

# 2. Verificar logs de IIS (últimos 100 errores)
Get-EventLog -LogName Application -Source "ASP.NET*" -Newest 100 -EntryType Error | 
    Where-Object { $_.Message -like "*OrdenRecaudacion*" }

# 3. Monitorear performance
# ... (usar herramienta de monitoring como Application Insights)

# 4. Verificar BD
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" `
    -h 172.20.16.55 `
    -U postgres `
    -d aocr_db `
    -c "SELECT COUNT(*) as total_ordenes FROM aocr_or_orden; 
        SELECT estado, COUNT(*) as cantidad FROM aocr_or_orden GROUP BY estado;"
```

---

## 🔄 ROLLBACK (Si algo sale mal)

```powershell
# 1. Revertir código
git revert HEAD
git push origin develop

# 2. Restaurar BD (si se agregaron columnas)
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" `
    -h 172.20.16.55 `
    -U postgres `
    -d aocr_db `
    -c "
    ALTER TABLE aocr_or_orden
    DROP COLUMN IF EXISTS observacion,
    DROP COLUMN IF EXISTS subtotal,
    DROP COLUMN IF EXISTS admin,
    DROP COLUMN IF EXISTS lugar_emision,
    DROP COLUMN IF EXISTS correo,
    DROP COLUMN IF EXISTS telefono,
    DROP COLUMN IF EXISTS concepto_id;
    
    ALTER TABLE aocr_or_orden_detalle
    DROP COLUMN IF EXISTS concepto_codigo,
    DROP COLUMN IF EXISTS descripcion,
    DROP COLUMN IF EXISTS porcentaje_admin,
    DROP COLUMN IF EXISTS subtotal,
    DROP COLUMN IF EXISTS admin;
    "

# 3. O restaurar desde backup completo
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" `
    -h 172.20.16.55 `
    -U postgres `
    -d aocr_db `
    -f backup_aocr_YYYYMMDD_HHMMSS.sql
```

---

## 📞 CONTACTOS Y RECURSOS

**Documentación:**
- [AUDITORIA_COMPLETA_ORDEN_RECAUDACION.md](AUDITORIA_COMPLETA_ORDEN_RECAUDACION.md)
- [RCA_ORDENES.md](RCA_ORDENES.md)
- [SOLUCION_TARIFAS_CONFIGURABLES.md](SOLUCION_TARIFAS_CONFIGURABLES.md)

**Scripts:**
- `scripts/fix_orden_recaudacion_sql.sql`
- `scripts/insert_parametros_tarifas.sql`
- Archivos `*_FIXED.cs` con código corregido

**Siguiente Paso:**
Decide qué opción elegir (1: agregar columnas, 2: simplificar código) y ejecuta la FASE 1.
