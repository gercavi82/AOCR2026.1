param(
    [string]$TaskName = "AOCR-ReporteErrores",
    [int]$Minutes = 30,
    [int]$DaysBack = 1
)

$ErrorActionPreference = "Stop"

if ($Minutes -lt 1) {
    throw "Minutes debe ser >= 1"
}

$scriptPath = Join-Path $PSScriptRoot "generar_log_errores.ps1"
if (-not (Test-Path $scriptPath)) {
    throw "No se encontró el script: $scriptPath"
}

$fullScriptPath = (Resolve-Path $scriptPath).Path
$taskArgs = "-NoProfile -ExecutionPolicy Bypass -File `"$fullScriptPath`" -DaysBack $DaysBack -ExportCsv -UseFixedOutputNames"
$taskCommand = "powershell.exe $taskArgs"

try {
    schtasks /Create `
        /TN $TaskName `
        /SC MINUTE `
        /MO $Minutes `
        /TR $taskCommand `
        /F | Out-Null
}
catch {
    throw "No se pudo registrar la tarea programada: $($_.Exception.Message)"
}

Write-Host "Tarea creada/actualizada: $TaskName"
Write-Host "Frecuencia: cada $Minutes minuto(s)"
Write-Host "Acción: $taskCommand"
Write-Host "Salida fija: CapaPresentacion/App_Data/Logs/REPORTE_ERRORES_ULTIMO.log y .csv"
