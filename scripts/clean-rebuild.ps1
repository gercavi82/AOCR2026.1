# Script de limpieza y rebuild para AOCR
# Ejecutar desde la raíz del proyecto

param(
    [string]$Configuration = "Release",
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logFile = "build_$timestamp.log"

Write-Host "=== AOCR - Limpieza y Rebuild ===" -ForegroundColor Cyan
Write-Host "Configuración: $Configuration"
Write-Host "Log: $logFile"

# 1. Eliminar carpetas bin/obj
Write-Host "`n[1/5] Eliminando carpetas bin/obj..." -ForegroundColor Yellow
$folders = Get-ChildItem -Path . -Include bin,obj -Recurse -Directory -Force
foreach ($folder in $folders) {
    if (Test-Path $folder.FullName) {
        Remove-Item -Path $folder.FullName -Recurse -Force -ErrorAction SilentlyContinue
        if ($Verbose) { Write-Host "  Eliminado: $($folder.FullName)" }
    }
}
Write-Host "  Carpetas eliminadas: $($folders.Count)" -ForegroundColor Green

# 2. Limpiar caché de NuGet local (opcional)
Write-Host "`n[2/5] Limpiando caché de NuGet..." -ForegroundColor Yellow
nuget locals http-cache -clear 2>&1 | Out-Null
Write-Host "  Cache limpiado" -ForegroundColor Green

# 3. Restaurar paquetes NuGet
Write-Host "`n[3/5] Restaurando paquetes NuGet..." -ForegroundColor Yellow
$nugetRestore = nuget restore AOCR.sln 2>&1
$nugetRestore | Out-File -FilePath $logFile -Append
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ERROR en restauración de NuGet" -ForegroundColor Red
    exit 1
}
Write-Host "  Paquetes restaurados" -ForegroundColor Green

# 4. Build
Write-Host "`n[4/5] Compilando solución ($Configuration)..." -ForegroundColor Yellow
$msbuildPath = "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
if (-not (Test-Path $msbuildPath)) {
    $msbuildPath = "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe"
}
if (-not (Test-Path $msbuildPath)) {
    $msbuildPath = "msbuild"
}

$buildOutput = & $msbuildPath AOCR.sln /t:Rebuild /p:Configuration=$Configuration /v:minimal /nologo 2>&1
$buildOutput | Out-File -FilePath $logFile -Append

# 5. Analizar resultados
Write-Host "`n[5/5] Analizando resultados..." -ForegroundColor Yellow
$errors = $buildOutput | Select-String -Pattern "error [A-Z]+[0-9]+:"
$warnings = $buildOutput | Select-String -Pattern "warning [A-Z]+[0-9]+:"

Write-Host "`n=== RESUMEN ===" -ForegroundColor Cyan
Write-Host "Errores: $($errors.Count)" -ForegroundColor $(if ($errors.Count -eq 0) { "Green" } else { "Red" })
Write-Host "Warnings: $($warnings.Count)" -ForegroundColor $(if ($warnings.Count -eq 0) { "Green" } else { "Yellow" })
Write-Host "Log guardado en: $logFile"

if ($errors.Count -gt 0) {
    Write-Host "`nErrores encontrados:" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host "`n✓ Build completado exitosamente" -ForegroundColor Green
