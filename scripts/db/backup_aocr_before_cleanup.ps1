param(
    [string]$DbHost = '172.20.16.55',
    [int]$Port = 5432,
    [string]$Database = 'dgac_des',
    [string]$Username = 'root',
    [Parameter(Mandatory = $true)]
    [string]$Password,
    [string]$OutputDir = $(Join-Path $PSScriptRoot 'backups'),
    [string]$PgDumpPath
)

$pgDump = $null

if (-not [string]::IsNullOrWhiteSpace($PgDumpPath)) {
    if (-not (Test-Path $PgDumpPath)) {
        throw ('No se encontro pg_dump en la ruta proporcionada: {0}' -f $PgDumpPath)
    }

    $pgDump = Get-Item $PgDumpPath
}
else {
    $pgDump = Get-Command pg_dump -ErrorAction SilentlyContinue

    if (-not $pgDump) {
        $candidatePaths = @(
            'C:\Program Files\PostgreSQL\18\bin\pg_dump.exe',
            'C:\Program Files\PostgreSQL\18\pgAdmin 4\runtime\pg_dump.exe',
            'C:\Program Files\PostgreSQL\17\bin\pg_dump.exe',
            'C:\Program Files\PostgreSQL\17\pgAdmin 4\runtime\pg_dump.exe',
            'C:\Program Files\PostgreSQL\16\bin\pg_dump.exe',
            'C:\Program Files\PostgreSQL\16\pgAdmin 4\runtime\pg_dump.exe'
        )

        $resolvedPath = $candidatePaths | Where-Object { Test-Path $_ } | Select-Object -First 1
        if ($resolvedPath) {
            $pgDump = Get-Item $resolvedPath
        }
    }
}

if (-not $pgDump) {
    throw 'No se encontro pg_dump ni en PATH ni en las rutas comunes de PostgreSQL/pgAdmin. Instale PostgreSQL client tools o indique -PgDumpPath.'
}

$pgDumpExecutable = if ($pgDump.PSObject.Properties['Source']) {
    $pgDump.Source
}
elseif ($pgDump.PSObject.Properties['FullName']) {
    $pgDump.FullName
}
else {
    $null
}

if ([string]::IsNullOrWhiteSpace($pgDumpExecutable)) {
    throw 'Se resolvio pg_dump, pero no fue posible obtener una ruta ejecutable valida.'
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$customDump = Join-Path $OutputDir ("backup_aocr_antes_limpieza_{0}.dump" -f $timestamp)
$plainDump = Join-Path $OutputDir ("backup_aocr_antes_limpieza_{0}.sql" -f $timestamp)

$env:PGPASSWORD = $Password

try {
    & $pgDumpExecutable -h $DbHost -p $Port -U $Username -d $Database -F c -f $customDump
    if ($LASTEXITCODE -ne 0) {
        throw 'pg_dump en formato custom fallo. No continue con la limpieza.'
    }

    & $pgDumpExecutable -h $DbHost -p $Port -U $Username -d $Database -f $plainDump
    if ($LASTEXITCODE -ne 0) {
        throw 'pg_dump en formato SQL plano fallo. No continue con la limpieza.'
    }

    if (-not (Test-Path $customDump) -or -not (Test-Path $plainDump)) {
        throw 'No se generaron ambos archivos de respaldo. No continue con la limpieza.'
    }

    Write-Host 'Respaldo completado correctamente.'
    Write-Host ('Custom dump: {0}' -f $customDump)
    Write-Host ('Plain SQL:   {0}' -f $plainDump)
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}