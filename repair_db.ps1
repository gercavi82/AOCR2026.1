# Script de reparacion de base de datos
Write-Host "=== REPARACION DE BASE DE DATOS ===" -ForegroundColor Cyan

# Informacion de conexion
$server = Read-Host "Servidor PostgreSQL (localhost)"
if (!$server) { $server = "localhost" }

$database = Read-Host "Nombre de la base de datos"
if (!$database) { 
    Write-Host "ERROR: Debe proporcionar el nombre de la base de datos" -ForegroundColor Red
    Read-Host "Presiona Enter para salir"
    exit
}

$username = Read-Host "Usuario (postgres)"
if (!$username) { $username = "postgres" }

$port = Read-Host "Puerto (5432)"
if (!$port) { $port = "5432" }

# Buscar psql
$psqlPath = $null
$paths = @(
    "C:\Program Files\PostgreSQL\16\bin\psql.exe",
    "C:\Program Files\PostgreSQL\15\bin\psql.exe",
    "C:\Program Files\PostgreSQL\14\bin\psql.exe",
    "C:\Program Files\PostgreSQL\13\bin\psql.exe"
)

foreach ($p in $paths) {
    if (Test-Path $p) {
        $psqlPath = $p
        break
    }
}

if (!$psqlPath) {
    $psqlPath = Read-Host "No se encontro PostgreSQL. Ruta completa a psql.exe"
    if (!(Test-Path $psqlPath)) {
        Write-Host "Ruta no valida" -ForegroundColor Red
        Read-Host
        exit
    }
}

$password = Read-Host "Contraseña" -AsSecureString
$plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))

Write-Host "Ejecutando reparacion..." -ForegroundColor Yellow

# Crear y ejecutar comandos uno por uno
$env:PGPASSWORD = $plainPassword

Write-Host "1. Agregando columnas necesarias..."
& $psqlPath -h $server -p $port -U $username -d $database -c "ALTER TABLE aocr_tbparametro ADD COLUMN IF NOT EXISTS codigoparametro VARCHAR(100) UNIQUE;"
& $psqlPath -h $server -p $port -U $username -d $database -c "ALTER TABLE aocr_tbparametro ADD COLUMN IF NOT EXISTS valorparametro DECIMAL(10,2);"
& $psqlPath -h $server -p $port -U $username -d $database -c "ALTER TABLE aocr_tbparametro ADD COLUMN IF NOT EXISTS descripcionparametro VARCHAR(255);"

Write-Host "2. Insertando parametros..."
& $psqlPath -h $server -p $port -U $username -d $database -c "INSERT INTO aocr_tbparametro (codigoparametro, valorparametro, descripcionparametro) VALUES ('CALCULO_VALOR_POR_ESTACION', 500.00, 'Valor por estacion para calculo de inspecciones') ON CONFLICT (codigoparametro) DO UPDATE SET valorparametro = EXCLUDED.valorparametro;"

& $psqlPath -h $server -p $port -U $username -d $database -c "INSERT INTO aocr_tbparametro (codigoparametro, valorparametro, descripcionparametro) VALUES ('CALCULO_VALOR_POR_DIA_VIATICO', 80.00, 'Valor por dia de viatico para inspectores') ON CONFLICT (codigoparametro) DO UPDATE SET valorparametro = EXCLUDED.valorparametro;"

& $psqlPath -h $server -p $port -U $username -d $database -c "INSERT INTO aocr_tbparametro (codigoparametro, valorparametro, descripcionparametro) VALUES ('CALCULO_PORCENTAJE_GASTOS_ADMIN', 8.00, 'Porcentaje de gastos administrativos') ON CONFLICT (codigoparametro) DO UPDATE SET valorparametro = EXCLUDED.valorparametro;"

Write-Host "3. Verificando resultados..."
& $psqlPath -h $server -p $port -U $username -d $database -c "SELECT codigoparametro, valorparametro FROM aocr_tbparametro WHERE codigoparametro IN ('CALCULO_VALOR_POR_ESTACION', 'CALCULO_VALOR_POR_DIA_VIATICO', 'CALCULO_PORCENTAJE_GASTOS_ADMIN');"

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nReparacion completada exitosamente!" -ForegroundColor Green
    Write-Host "Reinicia la aplicacion web para aplicar los cambios." -ForegroundColor Yellow
} else {
    Write-Host "`nHubo errores en la reparacion." -ForegroundColor Red
}

Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
Read-Host "Presiona Enter para continuar"