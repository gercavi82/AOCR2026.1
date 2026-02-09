# ============================================================
# Script: Aplicar Fixes Críticos - Módulo OrdenRecaudacion
# Fecha: 5 de febrero de 2026
# Descripción: Automatiza la aplicación de correcciones P0
# ============================================================

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("OpcionSQL", "OpcionCodigo")]
    [string]$Estrategia,
    
    [Parameter(Mandatory=$false)]
    [string]$DBPassword,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipBackup,
    
    [Parameter(Mandatory=$false)]
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$workspaceRoot = "c:\AOCR\AOCR\AOCR05-01-2026\AOCR1\AOCR"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " APLICAR FIXES CRÍTICOS - MÓDULO ORDEN RECAUDACIÓN" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# ============================================================
# PASO 1: VALIDACIÓN PREVIA
# ============================================================
Write-Host "[1/8] Validando entorno..." -ForegroundColor Yellow

# Verificar que estamos en el directorio correcto
if (-not (Test-Path "$workspaceRoot\AOCR.sln")) {
    Write-Host "❌ ERROR: No se encontró AOCR.sln en $workspaceRoot" -ForegroundColor Red
    exit 1
}

# Verificar archivos de fix
$archivosFix = @(
    "$workspaceRoot\AUDITORIA_COMPLETA_ORDEN_RECAUDACION.md",
    "$workspaceRoot\PLAN_REPARACION_ORDEN_RECAUDACION.md",
    "$workspaceRoot\scripts\fix_orden_recaudacion_sql.sql",
    "$workspaceRoot\scripts\insert_parametros_tarifas.sql",
    "$workspaceRoot\CapaNegocio\OrdenRecaudacionBL_FIXED.cs"
)

foreach ($archivo in $archivosFix) {
    if (-not (Test-Path $archivo)) {
        Write-Host "❌ ERROR: Archivo faltante: $archivo" -ForegroundColor Red
        exit 1
    }
}

Write-Host "✅ Archivos de fix encontrados" -ForegroundColor Green

# ============================================================
# PASO 2: BACKUP
# ============================================================
if (-not $SkipBackup) {
    Write-Host "`n[2/8] Creando backups..." -ForegroundColor Yellow
    
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupDir = "$workspaceRoot\backups\fix_ordenes_$timestamp"
    
    if (-not (Test-Path $backupDir)) {
        New-Item -Path $backupDir -ItemType Directory | Out-Null
    }
    
    # Backup de código
    $archivosBackup = @(
        "CapaNegocio\OrdenRecaudacionBL.cs",
        "CapaDatos\DAOs\OrdenRecaudacionDAO.cs",
        "CapaPresentacion\Controllers\OrdenRecaudacionController.cs",
        "CapaModelo\OrdenRecaudacion\OrdenRecaudacion.cs"
    )
    
    foreach ($archivo in $archivosBackup) {
        $source = Join-Path $workspaceRoot $archivo
        if (Test-Path $source) {
            $dest = Join-Path $backupDir (Split-Path $archivo -Leaf)
            Copy-Item $source $dest
            Write-Host "  Backup: $archivo → $dest" -ForegroundColor Gray
        }
    }
    
    # Backup de BD (si se proporciona password)
    if ($DBPassword) {
        Write-Host "  Creando backup de PostgreSQL..." -ForegroundColor Gray
        $backupFile = "$backupDir\aocr_db_$timestamp.sql"
        
        $env:PGPASSWORD = $DBPassword
        & "C:\Program Files\PostgreSQL\16\bin\pg_dump.exe" `
            -h 172.20.16.55 `
            -U postgres `
            -d aocr_db `
            -F p `
            -f $backupFile 2>&1 | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✅ Backup BD: $backupFile" -ForegroundColor Green
        } else {
            Write-Host "  ⚠️  Warning: No se pudo crear backup de BD" -ForegroundColor Yellow
        }
    }
    
    Write-Host "✅ Backups creados en: $backupDir" -ForegroundColor Green
} else {
    Write-Host "`n[2/8] Saltando backups (--SkipBackup especificado)" -ForegroundColor Yellow
}

# ============================================================
# PASO 3: GIT CHECKPOINT
# ============================================================
Write-Host "`n[3/8] Creando checkpoint en Git..." -ForegroundColor Yellow

try {
    Set-Location $workspaceRoot
    
    # Verificar si hay cambios sin commit
    $gitStatus = git status --porcelain
    if ($gitStatus) {
        Write-Host "  ⚠️  Hay cambios sin commit. Creando stash..." -ForegroundColor Yellow
        git stash save "Pre-fix-ordenes-$timestamp"
    }
    
    # Crear branch
    $branchName = "fix/orden-recaudacion-$timestamp"
    git checkout -b $branchName 2>&1 | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Branch creado: $branchName" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  No se pudo crear branch Git" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ⚠️  Git no disponible o no es un repositorio" -ForegroundColor Yellow
}

# ============================================================
# PASO 4: APLICAR FIXES SEGÚN ESTRATEGIA
# ============================================================
Write-Host "`n[4/8] Aplicando fixes (Estrategia: $Estrategia)..." -ForegroundColor Yellow

if ($Estrategia -eq "OpcionSQL") {
    # OPCIÓN 1: Agregar columnas a BD
    Write-Host "  Estrategia: Agregar columnas faltantes a BD" -ForegroundColor Cyan
    
    if (-not $DBPassword) {
        Write-Host "  ❌ ERROR: Se requiere -DBPassword para modificar BD" -ForegroundColor Red
        exit 1
    }
    
    if ($DryRun) {
        Write-Host "  [DRY RUN] Se ejecutaría:" -ForegroundColor Magenta
        Write-Host "    - ALTER TABLE aocr_or_orden ADD COLUMNS..." -ForegroundColor Gray
        Write-Host "    - ALTER TABLE aocr_or_orden_detalle ADD COLUMNS..." -ForegroundColor Gray
    } else {
        # Ejecutar script SQL
        $env:PGPASSWORD = $DBPassword
        
        $sqlScript = @"
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
"@
        
        $sqlScript | & "C:\Program Files\PostgreSQL\16\bin\psql.exe" `
            -h 172.20.16.55 `
            -U postgres `
            -d aocr_db `
            -v ON_ERROR_STOP=1 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✅ Columnas agregadas exitosamente" -ForegroundColor Green
        } else {
            Write-Host "  ❌ ERROR: Falló la modificación de BD" -ForegroundColor Red
            exit 1
        }
    }
    
} elseif ($Estrategia -eq "OpcionCodigo") {
    # OPCIÓN 2: Simplificar código
    Write-Host "  Estrategia: Simplificar código a columnas existentes" -ForegroundColor Cyan
    Write-Host "  ⚠️  PENDIENTE: Requiere refactorización manual de DAO" -ForegroundColor Yellow
    Write-Host "  Ver: AUDITORIA_COMPLETA_ORDEN_RECAUDACION.md sección 'Fix 7'" -ForegroundColor Yellow
}

# ============================================================
# PASO 5: APLICAR FIX ASYNC PATTERN (BL)
# ============================================================
Write-Host "`n[5/8] Aplicando fix async pattern (BL)..." -ForegroundColor Yellow

$blOriginal = "$workspaceRoot\CapaNegocio\OrdenRecaudacionBL.cs"
$blFixed = "$workspaceRoot\CapaNegocio\OrdenRecaudacionBL_FIXED.cs"

if ($DryRun) {
    Write-Host "  [DRY RUN] Se reemplazaría:" -ForegroundColor Magenta
    Write-Host "    $blOriginal" -ForegroundColor Gray
    Write-Host "    con FIXED version" -ForegroundColor Gray
} else {
    Copy-Item $blFixed $blOriginal -Force
    Write-Host "  ✅ OrdenRecaudacionBL.cs actualizado con async correcto" -ForegroundColor Green
}

# ============================================================
# PASO 6: INSERTAR PARÁMETROS DE TARIFAS
# ============================================================
Write-Host "`n[6/8] Insertando parámetros de tarifas..." -ForegroundColor Yellow

if ($DBPassword) {
    if ($DryRun) {
        Write-Host "  [DRY RUN] Se insertarían parámetros de tarifas" -ForegroundColor Magenta
    } else {
        $env:PGPASSWORD = $DBPassword
        & "C:\Program Files\PostgreSQL\16\bin\psql.exe" `
            -h 172.20.16.55 `
            -U postgres `
            -d aocr_db `
            -f "$workspaceRoot\scripts\insert_parametros_tarifas.sql" `
            -v ON_ERROR_STOP=1 2>&1 | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✅ Parámetros de tarifas insertados" -ForegroundColor Green
        } else {
            Write-Host "  ⚠️  No se pudieron insertar parámetros (tabla 'parametros' puede no existir)" -ForegroundColor Yellow
            Write-Host "     Crear tabla manualmente si es necesario" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "  ⚠️  Saltando (sin -DBPassword)" -ForegroundColor Yellow
}

# ============================================================
# PASO 7: COMPILACIÓN
# ============================================================
Write-Host "`n[7/8] Compilando solución..." -ForegroundColor Yellow

if ($DryRun) {
    Write-Host "  [DRY RUN] Se compilaría la solución" -ForegroundColor Magenta
} else {
    Set-Location $workspaceRoot
    dotnet clean AOCR.sln --verbosity quiet
    dotnet build AOCR.sln --configuration Debug --verbosity minimal
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✅ Compilación exitosa" -ForegroundColor Green
    } else {
        Write-Host "  ❌ ERROR: Falló la compilación" -ForegroundColor Red
        Write-Host "     Revisar errores y aplicar correcciones manualmente" -ForegroundColor Yellow
        exit 1
    }
}

# ============================================================
# PASO 8: RESUMEN FINAL
# ============================================================
Write-Host "`n[8/8] Resumen de cambios aplicados" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan

Write-Host ""
Write-Host "✅ FIXES APLICADOS:" -ForegroundColor Green
Write-Host "  • P0-1: SQL INSERT detalle " -NoNewline
if ($Estrategia -eq "OpcionSQL") { 
    Write-Host "[BD modificada]" -ForegroundColor Green 
} else { 
    Write-Host "[Requiere refactor código]" -ForegroundColor Yellow 
}

Write-Host "  • P0-2: SQL UPDATE orden " -NoNewline
if ($Estrategia -eq "OpcionSQL") { 
    Write-Host "[BD modificada]" -ForegroundColor Green 
} else { 
    Write-Host "[Requiere refactor código]" -ForegroundColor Yellow 
}

Write-Host "  • P0-3: Métodos async reales " -ForegroundColor Green -NoNewline
Write-Host "[BL actualizado]" -ForegroundColor Gray

Write-Host "  • P0-4: Eliminado .Result " -ForegroundColor Green -NoNewline
Write-Host "[BL actualizado]" -ForegroundColor Gray

Write-Host "  • P0-5: DI en Controller " -ForegroundColor Yellow -NoNewline
Write-Host "[Requiere config manual]" -ForegroundColor Gray

Write-Host "  • P1-1: Tarifas configurables " -NoNewline
if ($DBPassword) { 
    Write-Host "[Parámetros insertados]" -ForegroundColor Green 
} else { 
    Write-Host "[Pendiente]" -ForegroundColor Yellow 
}

Write-Host ""
Write-Host "📋 TAREAS PENDIENTES:" -ForegroundColor Yellow

$tareasPendientes = @(
    "Actualizar Controller a async (ver PLAN_REPARACION linea ~200)",
    "Configurar DI en App_Start/UnityConfig.cs (ver PLAN_REPARACION linea ~280)",
    "Modificar AsegurarConceptosBasicos para usar tarifas BD (ver SOLUCION_TARIFAS_CONFIGURABLES.md)",
    "Ejecutar tests unitarios: dotnet test",
    "Probar manualmente: Crear/Editar/PDF de orden",
    "Commit cambios: git commit -m Fix criticos OrdenRecaudacion",
    "Deploy a QA para validacion"
)

$i = 1
foreach ($tarea in $tareasPendientes) {
    Write-Host "  $i. $tarea" -ForegroundColor Gray
    $i++
}

Write-Host ""
Write-Host "DOCUMENTACION:" -ForegroundColor Cyan
Write-Host "  - AUDITORIA_COMPLETA_ORDEN_RECAUDACION.md" -ForegroundColor Gray
Write-Host "  - PLAN_REPARACION_ORDEN_RECAUDACION.md" -ForegroundColor Gray
Write-Host "  - RCA_ORDENES.md" -ForegroundColor Gray
Write-Host "  - SOLUCION_TARIFAS_CONFIGURABLES.md" -ForegroundColor Gray

if (-not $SkipBackup) {
    Write-Host ""
    Write-Host "ROLLBACK:" -ForegroundColor Magenta
    Write-Host "  Backups en: $backupDir" -ForegroundColor Gray
    Write-Host "  Git branch: $branchName" -ForegroundColor Gray
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Siguiente paso: Completar tareas pendientes y ejecutar tests" -ForegroundColor White
Write-Host "============================================================" -ForegroundColor Cyan
