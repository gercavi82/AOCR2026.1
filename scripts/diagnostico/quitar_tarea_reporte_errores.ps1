param(
    [string]$TaskName = "AOCR-ReporteErrores"
)

$ErrorActionPreference = "Stop"

schtasks /Delete /TN $TaskName /F | Out-Null
Write-Host "Tarea eliminada: $TaskName"
