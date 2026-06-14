# T-24h: backup BD + carpeta aplicación antes de go-live
param(
    [string]$PublishPath = "C:\AOCR\publicacion1",
    [string]$BackupRoot = "C:\AOCR\backups\golive",
    [string]$DbHost = "172.20.16.55",
    [int]$DbPort = 5432,
    [string]$Database = "dgac_des",
    [string]$Username = "root",
    [Parameter(Mandatory = $true)]
    [string]$Password
)

$ErrorActionPreference = "Stop"
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$dest = Join-Path $BackupRoot $ts
New-Item -ItemType Directory -Force -Path $dest | Out-Null

Write-Host "=== Backup pre-deploy AOCR $ts ===" -ForegroundColor Cyan

# 1. BD
$backupScript = Join-Path $PSScriptRoot "..\db\backup_aocr_before_cleanup.ps1"
if (-not (Test-Path $backupScript)) {
    throw "No se encontró $backupScript"
}

$dbOut = Join-Path $dest "database"
New-Item -ItemType Directory -Force -Path $dbOut | Out-Null
& $backupScript -DbHost $DbHost -Port $DbPort -Database $Database -Username $Username -Password $Password -OutputDir $dbOut
Write-Host "[OK] Backup BD en $dbOut" -ForegroundColor Green

# 2. Carpeta aplicación (robocopy mirror)
if (Test-Path $PublishPath) {
    $appDest = Join-Path $dest "aplicacion"
    New-Item -ItemType Directory -Force -Path $appDest | Out-Null
    robocopy $PublishPath $appDest /MIR /XD "App_Data\Logs" /R:2 /W:2 /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy falló con código $LASTEXITCODE" }
    Write-Host "[OK] Copia aplicación en $appDest" -ForegroundColor Green
} else {
    Write-Warning "PublishPath no existe: $PublishPath"
}

# 3. Manifiesto
$manifest = @{
    timestamp   = $ts
    publishPath = $PublishPath
    database    = "$DbHost`:$DbPort/$Database"
    hostname    = $env:COMPUTERNAME
    user        = $env:USERNAME
} | ConvertTo-Json
$manifest | Out-File (Join-Path $dest "MANIFEST.json") -Encoding utf8

Write-Host ""
Write-Host "Backup completo: $dest" -ForegroundColor Green
Write-Host "Conservar este path para rollback."
