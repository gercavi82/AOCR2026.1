# Script para resolver el problema de DLL bloqueada en la compilación
Write-Host "=== SOLUCIONANDO PROBLEMA DE DLL BLOQUEADA ===" -ForegroundColor Cyan

# 1. Cerrar procesos que puedan estar bloqueando archivos
Write-Host "1. Cerrando procesos PowerShell innecesarios..." -ForegroundColor Yellow
$currentPID = $PID
Get-Process -Name "powershell" -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $currentPID } | ForEach-Object {
    try {
        Write-Host "   Cerrando PowerShell PID: $($_.Id)" -ForegroundColor Gray
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    } catch {
        Write-Host "   No se pudo cerrar PID $($_.Id)" -ForegroundColor Red
    }
}

# 2. Verificar si Visual Studio está ejecutándose
$vsProcess = Get-Process -Name "*devenv*" -ErrorAction SilentlyContinue
if ($vsProcess) {
    Write-Host "2. Visual Studio está ejecutándose (PID: $($vsProcess.Id))" -ForegroundColor Yellow
    Write-Host "   RECOMENDACIÓN: Cierra Visual Studio manualmente para una limpieza completa" -ForegroundColor Red
} else {
    Write-Host "2. Visual Studio no está ejecutándose" -ForegroundColor Green
}

# 3. Limpiar archivos compilados
Write-Host "3. Limpiando archivos compilados..." -ForegroundColor Yellow

$projectRoot = "c:\AOCR\AOCR\AOCR05-01-2026\AOCR1\AOCR"

# Limpiar carpetas bin y obj
$foldersToClean = @("bin", "obj")
$projectDirs = @("CapaDatos", "CapaModelo", "CapaNegocio", "CapaPresentacion", "AOCR.Tests")

foreach ($projectDir in $projectDirs) {
    $projectPath = Join-Path $projectRoot $projectDir
    if (Test-Path $projectPath) {
        foreach ($folder in $foldersToClean) {
            $cleanPath = Join-Path $projectPath $folder
            if (Test-Path $cleanPath) {
                Write-Host "   Limpiando: $cleanPath" -ForegroundColor Gray
                try {
                    Remove-Item -Path "$cleanPath\*" -Recurse -Force -ErrorAction SilentlyContinue
                } catch {
                    Write-Host "   Error limpiando: $cleanPath" -ForegroundColor Red
                }
            }
        }
    }
}

# 4. Limpiar archivos específicos problemáticos
Write-Host "4. Eliminando archivos DLL específicos..." -ForegroundColor Yellow
$dllsToRemove = @(
    "$projectRoot\CapaDatos\bin\Debug\CapaDatos.dll",
    "$projectRoot\CapaDatos\obj\Debug\CapaDatos.dll",
    "$projectRoot\CapaPresentacion\bin\CapaDatos.dll"
)

foreach ($dll in $dllsToRemove) {
    if (Test-Path $dll) {
        try {
            Write-Host "   Eliminando: $dll" -ForegroundColor Gray
            Remove-Item -Path $dll -Force
            Write-Host "   ✓ Eliminado exitosamente" -ForegroundColor Green
        } catch {
            Write-Host "   ✗ Error eliminando: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

# 5. Verificar que los archivos fueron eliminados
Write-Host "5. Verificando limpieza..." -ForegroundColor Yellow
$remainingDlls = Get-ChildItem -Path $projectRoot -Recurse -Filter "CapaDatos.dll" -ErrorAction SilentlyContinue
if ($remainingDlls.Count -eq 0) {
    Write-Host "   ✓ Todos los archivos CapaDatos.dll eliminados" -ForegroundColor Green
} else {
    Write-Host "   ⚠ Algunos archivos DLL permanecen:" -ForegroundColor Yellow
    $remainingDlls | ForEach-Object { Write-Host "     - $($_.FullName)" -ForegroundColor Gray }
}

# 6. Instrucciones finales
Write-Host "`n=== INSTRUCCIONES PARA CONTINUAR ===" -ForegroundColor Cyan
Write-Host "1. Si Visual Studio está abierto, ciérralo completamente" -ForegroundColor White
Write-Host "2. Abre Visual Studio como Administrador" -ForegroundColor White
Write-Host "3. Abre la solución AOCR.sln" -ForegroundColor White
Write-Host "4. Ve a Build → Clean Solution" -ForegroundColor White
Write-Host "5. Ve a Build → Rebuild Solution" -ForegroundColor White
Write-Host "6. Si persiste el error, reinicia el sistema" -ForegroundColor White

Write-Host "`n✅ Script completado. Ahora puedes intentar compilar nuevamente." -ForegroundColor Green