# Ejecuta limpieza operacional AOCR preservando usuarios y seguridad.
param(
    [string]$ConnectionString = "Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=control;Timeout=30;Command Timeout=600",
    [string]$RepoRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)
)

$ErrorActionPreference = "Stop"
$binPath = Join-Path $RepoRoot "CapaDatos\bin\Release"
if (-not (Test-Path $binPath)) {
    throw "No se encontro $binPath. Compile CapaDatos en Release primero."
}

Get-ChildItem $binPath -Filter "*.dll" | ForEach-Object {
    try { Add-Type -Path $_.FullName -ErrorAction SilentlyContinue | Out-Null } catch {}
}
Add-Type -Path (Join-Path $binPath "Npgsql.dll")

function Invoke-PgSqlFile {
    param(
        [Npgsql.NpgsqlConnection]$Connection,
        [string]$FilePath
    )

    if (-not (Test-Path $FilePath)) {
        throw "No existe el archivo SQL: $FilePath"
    }

    $sql = Get-Content -Path $FilePath -Raw -Encoding UTF8
    $cmd = $Connection.CreateCommand()
    $cmd.CommandTimeout = 600
    $cmd.CommandText = $sql
    try {
        [void]$cmd.ExecuteNonQuery()
    }
    finally {
        $cmd.Dispose()
    }
}

function Get-TableCount {
    param(
        [Npgsql.NpgsqlConnection]$Connection,
        [string]$TableName
    )

    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM public.$TableName"
    try { return [int64]$cmd.ExecuteScalar() }
    catch { return -1 }
    finally { $cmd.Dispose() }
}

$cleanupSql = Join-Path $PSScriptRoot "aocr_operational_cleanup_commit.sql"
$sequencesSql = Join-Path $PSScriptRoot "aocr_reset_operational_sequences.sql"

Write-Host "=== AOCR - Limpieza operacional PostgreSQL ===" -ForegroundColor Cyan
Write-Host "Base: dgac_des @ 172.20.16.55"

$conn = New-Object Npgsql.NpgsqlConnection($ConnectionString)
$conn.Open()

try {
    $usersBefore = Get-TableCount -Connection $conn -TableName "usuario"
    $solicitudesBefore = Get-TableCount -Connection $conn -TableName "aocr_tbsolicitud"
    Write-Host "Antes: usuario=$usersBefore, aocr_tbsolicitud=$solicitudesBefore"

    Write-Host "Ejecutando limpieza operacional..." -ForegroundColor Yellow
    Invoke-PgSqlFile -Connection $conn -FilePath $cleanupSql

    Write-Host "Reiniciando secuencias operativas..." -ForegroundColor Yellow
    Invoke-PgSqlFile -Connection $conn -FilePath $sequencesSql

    $usersAfter = Get-TableCount -Connection $conn -TableName "usuario"
    $rolesAfter = Get-TableCount -Connection $conn -TableName "rol"
    $solicitudesAfter = Get-TableCount -Connection $conn -TableName "aocr_tbsolicitud"
    $documentosAfter = Get-TableCount -Connection $conn -TableName "aocr_tbdocumento"
    $inspeccionesAfter = Get-TableCount -Connection $conn -TableName "aocr_tbinspeccion"
    $ordenesAfter = Get-TableCount -Connection $conn -TableName "aocr_or_orden"

    Write-Host ""
    Write-Host "=== Resultado ===" -ForegroundColor Green
    Write-Host "usuario (conservados): $usersAfter"
    Write-Host "rol (conservados): $rolesAfter"
    Write-Host "aocr_tbsolicitud: $solicitudesAfter"
    Write-Host "aocr_tbdocumento: $documentosAfter"
    Write-Host "aocr_tbinspeccion: $inspeccionesAfter"
    Write-Host "aocr_or_orden: $ordenesAfter"
    Write-Host "Limpieza completada." -ForegroundColor Green
}
finally {
    if ($conn.State -eq [System.Data.ConnectionState]::Open) {
        $conn.Close()
    }
    $conn.Dispose()
}
