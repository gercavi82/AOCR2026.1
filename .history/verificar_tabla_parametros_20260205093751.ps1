# Script para verificar y corregir la tabla aocr_tbparametro

Write-Host "=== VERIFICACIÓN Y CORRECCIÓN DE TABLA AOCR_TBPARAMETRO ===" -ForegroundColor Cyan

# Configuración de la conexión (ajustar según sea necesario)
$server = "localhost"
$database = "tu_base_datos"  # Cambiar por el nombre real de la base de datos
$username = "postgres"       # Cambiar por tu usuario
$port = "5432"

Write-Host "`nPor favor, proporciona la información de conexión:" -ForegroundColor Yellow
$server = Read-Host "Servidor PostgreSQL (presiona Enter para localhost)"
if ([string]::IsNullOrWhiteSpace($server)) { $server = "localhost" }

$database = Read-Host "Nombre de la base de datos"
if ([string]::IsNullOrWhiteSpace($database)) { 
    Write-Host "ERROR: Debe proporcionar el nombre de la base de datos" -ForegroundColor Red
    exit 1
}

$username = Read-Host "Usuario (presiona Enter para postgres)"
if ([string]::IsNullOrWhiteSpace($username)) { $username = "postgres" }

$port = Read-Host "Puerto (presiona Enter para 5432)"
if ([string]::IsNullOrWhiteSpace($port)) { $port = "5432" }

# Buscar psql en rutas comunes
$psqlPaths = @(
    "C:\Program Files\PostgreSQL\16\bin\psql.exe",
    "C:\Program Files\PostgreSQL\15\bin\psql.exe",
    "C:\Program Files\PostgreSQL\14\bin\psql.exe",
    "C:\Program Files\PostgreSQL\13\bin\psql.exe",
    "C:\Program Files (x86)\PostgreSQL\16\bin\psql.exe",
    "C:\Program Files (x86)\PostgreSQL\15\bin\psql.exe"
)

$psqlPath = $null
foreach ($path in $psqlPaths) {
    if (Test-Path $path) {
        $psqlPath = $path
        break
    }
}

if ($psqlPath -eq $null) {
    Write-Host "ERROR: No se encontró psql.exe en las rutas comunes de PostgreSQL." -ForegroundColor Red
    Write-Host "Por favor, instala PostgreSQL o proporciona la ruta manualmente." -ForegroundColor Yellow
    $customPath = Read-Host "Ruta completa a psql.exe (o presiona Enter para salir)"
    if ([string]::IsNullOrWhiteSpace($customPath)) {
        exit 1
    }
    if (Test-Path $customPath) {
        $psqlPath = $customPath
    } else {
        Write-Host "ERROR: Ruta no válida: $customPath" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`nUsando psql: $psqlPath" -ForegroundColor Green

# Función para ejecutar comandos SQL
function Invoke-PostgreSQLCommand {
    param(
        [string]$Command,
        [string]$Description
    )
    
    Write-Host "`n--- $Description ---" -ForegroundColor Cyan
    Write-Host "Ejecutando: $Command" -ForegroundColor Gray
    
    $env:PGPASSWORD = $password
    $result = & $psqlPath -h $server -p $port -U $username -d $database -c $Command
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Comando ejecutado exitosamente" -ForegroundColor Green
        return $result
    } else {
        Write-Host "✗ Error ejecutando comando" -ForegroundColor Red
        return $null
    }
}

# Solicitar contraseña
$password = Read-Host "Contraseña para $username" -AsSecureString
$password = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))

Write-Host "`n=== VERIFICANDO ESTRUCTURA DE LA TABLA ===" -ForegroundColor Yellow

# Verificar si la tabla existe
$tableExists = Invoke-PostgreSQLCommand -Command "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'aocr_tbparametro');" -Description "Verificando existencia de tabla aocr_tbparametro"

# Mostrar estructura actual de la tabla
$tableStructure = Invoke-PostgreSQLCommand -Command "\d aocr_tbparametro" -Description "Estructura actual de aocr_tbparametro"

Write-Host "`n=== VERIFICANDO COLUMNAS NECESARIAS ===" -ForegroundColor Yellow

# Verificar columnas existentes
$columns = Invoke-PostgreSQLCommand -Command "SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'aocr_tbparametro' ORDER BY ordinal_position;" -Description "Columnas existentes"

Write-Host "`n=== CORRIGIENDO ESTRUCTURA SI ES NECESARIO ===" -ForegroundColor Yellow

# Verificar si existe la columna codigoparametro
$columnExists = Invoke-PostgreSQLCommand -Command "SELECT EXISTS (SELECT FROM information_schema.columns WHERE table_name = 'aocr_tbparametro' AND column_name = 'codigoparametro');" -Description "Verificando columna codigoparametro"

if ($columnExists -like "*f*" -or $columnExists -like "*false*") {
    Write-Host "`n⚠️  La columna 'codigoparametro' NO existe. Creándola..." -ForegroundColor Yellow
    
    $addColumn = Invoke-PostgreSQLCommand -Command "ALTER TABLE aocr_tbparametro ADD COLUMN IF NOT EXISTS codigoparametro VARCHAR(100) UNIQUE;" -Description "Agregando columna codigoparametro"
    
    if ($addColumn -ne $null) {
        Write-Host "✓ Columna 'codigoparametro' agregada exitosamente" -ForegroundColor Green
    }
} else {
    Write-Host "✓ La columna 'codigoparametro' ya existe" -ForegroundColor Green
}

# Verificar otras columnas necesarias
$requiredColumns = @("valorparametro", "descripcionparametro")
foreach ($column in $requiredColumns) {
    $exists = Invoke-PostgreSQLCommand -Command "SELECT EXISTS (SELECT FROM information_schema.columns WHERE table_name = 'aocr_tbparametro' AND column_name = '$column');" -Description "Verificando columna $column"
    
    if ($exists -like "*f*" -or $exists -like "*false*") {
        Write-Host "⚠️  La columna '$column' NO existe" -ForegroundColor Yellow
        
        if ($column -eq "valorparametro") {
            Invoke-PostgreSQLCommand -Command "ALTER TABLE aocr_tbparametro ADD COLUMN IF NOT EXISTS valorparametro DECIMAL(10,2);" -Description "Agregando columna $column"
        } elseif ($column -eq "descripcionparametro") {
            Invoke-PostgreSQLCommand -Command "ALTER TABLE aocr_tbparametro ADD COLUMN IF NOT EXISTS descripcionparametro VARCHAR(255);" -Description "Agregando columna $column"
        }
    } else {
        Write-Host "✓ La columna '$column' existe" -ForegroundColor Green
    }
}

Write-Host "`n=== INSERTANDO PARÁMETROS NECESARIOS ===" -ForegroundColor Yellow

# SQL para insertar los parámetros necesarios
$insertSQL = @"
-- Insertar parámetros de cálculo para PDF
INSERT INTO aocr_tbparametro (codigoparametro, valorparametro, descripcionparametro) 
VALUES 
    ('CALCULO_VALOR_POR_ESTACION', 500.00, 'Valor por estación para cálculo de inspecciones')
ON CONFLICT (codigoparametro) DO UPDATE SET 
    valorparametro = EXCLUDED.valorparametro,
    descripcionparametro = EXCLUDED.descripcionparametro;

INSERT INTO aocr_tbparametro (codigoparametro, valorparametro, descripcionparametro) 
VALUES 
    ('CALCULO_VALOR_POR_DIA_VIATICO', 80.00, 'Valor por día de viático para inspectores')
ON CONFLICT (codigoparametro) DO UPDATE SET 
    valorparametro = EXCLUDED.valorparametro,
    descripcionparametro = EXCLUDED.descripcionparametro;

INSERT INTO aocr_tbparametro (codigoparametro, valorparametro, descripcionparametro) 
VALUES 
    ('CALCULO_PORCENTAJE_GASTOS_ADMIN', 8.00, 'Porcentaje de gastos administrativos (como porcentaje, ej: 8 para 8%)')
ON CONFLICT (codigoparametro) DO UPDATE SET 
    valorparametro = EXCLUDED.valorparametro,
    descripcionparametro = EXCLUDED.descripcionparametro;
"@

$insertResult = Invoke-PostgreSQLCommand -Command $insertSQL -Description "Insertando parámetros de cálculo"

Write-Host "`n=== VERIFICANDO PARÁMETROS INSERTADOS ===" -ForegroundColor Yellow

$parametersCheck = Invoke-PostgreSQLCommand -Command "SELECT codigoparametro, valorparametro, descripcionparametro FROM aocr_tbparametro WHERE codigoparametro IN ('CALCULO_VALOR_POR_ESTACION', 'CALCULO_VALOR_POR_DIA_VIATICO', 'CALCULO_PORCENTAJE_GASTOS_ADMIN') ORDER BY codigoparametro;" -Description "Verificando parámetros insertados"

Write-Host "`n=== RESUMEN ===" -ForegroundColor Cyan
Write-Host "✓ Estructura de tabla verificada y corregida" -ForegroundColor Green
Write-Host "✓ Parámetros de cálculo insertados" -ForegroundColor Green
Write-Host "`n🔄 Ahora puedes reiniciar la aplicación web para que tome los nuevos parámetros." -ForegroundColor Yellow
Write-Host "`n📝 Los valores insertados son:" -ForegroundColor White
Write-Host "   - CALCULO_VALOR_POR_ESTACION: 500.00" -ForegroundColor Gray
Write-Host "   - CALCULO_VALOR_POR_DIA_VIATICO: 80.00" -ForegroundColor Gray  
Write-Host "   - CALCULO_PORCENTAJE_GASTOS_ADMIN: 8.00" -ForegroundColor Gray

Write-Host "`nPresiona cualquier tecla para continuar..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")