# Script para verificar y corregir la tabla aocr_tbparametro

Write-Host "=== VERIFICACIÓN Y CORRECCIÓN DE TABLA AOCR_TBPARAMETRO ===" -ForegroundColor Cyan

# Configuración de la conexión (ajustar según sea necesario)
$server = "localhost"
$database = "tu_base_datos"  # Cambiar por el nombre real de la base de datos
$username = "postgres"       # Cambiar por tu usuario
$port = "5432"

Write-Host "`nPor favor, proporciona la información de conexión:" -ForegroundColor Yellow
$serverInput = Read-Host "Servidor PostgreSQL (presiona Enter para localhost)"
if (![string]::IsNullOrWhiteSpace($serverInput)) { $server = $serverInput }

$databaseInput = Read-Host "Nombre de la base de datos"
if ([string]::IsNullOrWhiteSpace($databaseInput)) { 
    Write-Host "ERROR: Debe proporcionar el nombre de la base de datos" -ForegroundColor Red
    exit 1
}
$database = $databaseInput

$usernameInput = Read-Host "Usuario (presiona Enter para postgres)"
if (![string]::IsNullOrWhiteSpace($usernameInput)) { $username = $usernameInput }

$portInput = Read-Host "Puerto (presiona Enter para 5432)"
if (![string]::IsNullOrWhiteSpace($portInput)) { $port = $portInput }

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

# Solicitar contraseña
$password = Read-Host "Contraseña para $username" -AsSecureString
$password = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))

Write-Host "`n=== VERIFICANDO ESTRUCTURA DE LA TABLA ===" -ForegroundColor Yellow

# Función para ejecutar comandos SQL
function Invoke-PostgreSQLCommand {
    param(
        [string]$Command,
        [string]$Description
    )
    
    Write-Host "`n--- $Description ---" -ForegroundColor Cyan
    Write-Host "Ejecutando: $Command" -ForegroundColor Gray
    
    $env:PGPASSWORD = $password
    $result = & $psqlPath -h $server -p $port -U $username -d $database -c $Command 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Comando ejecutado exitosamente" -ForegroundColor Green
        Write-Host $result
        return $result
    } else {
        Write-Host "✗ Error ejecutando comando" -ForegroundColor Red
        Write-Host $result
        return $null
    }
}

# Verificar si la tabla existe
$tableExistsCmd = "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'aocr_tbparametro');"
$tableExists = Invoke-PostgreSQLCommand -Command $tableExistsCmd -Description "Verificando existencia de tabla aocr_tbparametro"

# Mostrar estructura actual de la tabla
$tableStructureCmd = "\d aocr_tbparametro"
$tableStructure = Invoke-PostgreSQLCommand -Command $tableStructureCmd -Description "Estructura actual de aocr_tbparametro"

Write-Host "`n=== VERIFICANDO COLUMNAS NECESARIAS ===" -ForegroundColor Yellow

# Verificar columnas existentes
$columnsCmd = "SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'aocr_tbparametro' ORDER BY ordinal_position;"
$columns = Invoke-PostgreSQLCommand -Command $columnsCmd -Description "Columnas existentes"

Write-Host "`n=== CORRIGIENDO ESTRUCTURA SI ES NECESARIO ===" -ForegroundColor Yellow

# Verificar si existe la columna codigoparametro
$columnExistsCmd = "SELECT EXISTS (SELECT FROM information_schema.columns WHERE table_name = 'aocr_tbparametro' AND column_name = 'codigoparametro');"
$columnExists = Invoke-PostgreSQLCommand -Command $columnExistsCmd -Description "Verificando columna codigoparametro"

if ($columnExists -like "*f*" -or $columnExists -like "*false*") {
    Write-Host "`n⚠️  La columna 'codigoparametro' NO existe. Creándola..." -ForegroundColor Yellow
    
    $addColumnCmd = "ALTER TABLE aocr_tbparametro ADD COLUMN IF NOT EXISTS codigoparametro VARCHAR(100) UNIQUE;"
    $addColumn = Invoke-PostgreSQLCommand -Command $addColumnCmd -Description "Agregando columna codigoparametro"
    
    if ($addColumn -ne $null) {
        Write-Host "✓ Columna 'codigoparametro' agregada exitosamente" -ForegroundColor Green
    }
} else {
    Write-Host "✓ La columna 'codigoparametro' ya existe" -ForegroundColor Green
}

# Verificar otras columnas necesarias
$requiredColumns = @("valorparametro", "descripcionparametro")
foreach ($column in $requiredColumns) {
    $existsCmd = "SELECT EXISTS (SELECT FROM information_schema.columns WHERE table_name = 'aocr_tbparametro' AND column_name = '$column');"
    $exists = Invoke-PostgreSQLCommand -Command $existsCmd -Description "Verificando columna $column"
    
    if ($exists -like "*f*" -or $exists -like "*false*") {
        Write-Host "⚠️  La columna '$column' NO existe" -ForegroundColor Yellow
        
        if ($column -eq "valorparametro") {
            $addColCmd = "ALTER TABLE aocr_tbparametro ADD COLUMN IF NOT EXISTS valorparametro DECIMAL(10,2);"
            Invoke-PostgreSQLCommand -Command $addColCmd -Description "Agregando columna $column"
        } elseif ($column -eq "descripcionparametro") {
            $addColCmd = "ALTER TABLE aocr_tbparametro ADD COLUMN IF NOT EXISTS descripcionparametro VARCHAR(255);"
            Invoke-PostgreSQLCommand -Command $addColCmd -Description "Agregando columna $column"
        }
    } else {
        Write-Host "✓ La columna '$column' existe" -ForegroundColor Green
    }
}

Write-Host "`n=== INSERTANDO PARÁMETROS NECESARIOS ===" -ForegroundColor Yellow

# SQL para insertar los parámetros necesarios
$insertSQL = "INSERT INTO aocr_tbparametro (codigoparametro, valorparametro, descripcionparametro) VALUES ('CALCULO_VALOR_POR_ESTACION', 500.00, 'Valor por estación para cálculo de inspecciones') ON CONFLICT (codigoparametro) DO UPDATE SET valorparametro = EXCLUDED.valorparametro, descripcionparametro = EXCLUDED.descripcionparametro;"

$insertResult1 = Invoke-PostgreSQLCommand -Command $insertSQL -Description "Insertando parámetro CALCULO_VALOR_POR_ESTACION"

$insertSQL2 = "INSERT INTO aocr_tbparametro (codigoparametro, valorparametro, descripcionparametro) VALUES ('CALCULO_VALOR_POR_DIA_VIATICO', 80.00, 'Valor por día de viático para inspectores') ON CONFLICT (codigoparametro) DO UPDATE SET valorparametro = EXCLUDED.valorparametro, descripcionparametro = EXCLUDED.descripcionparametro;"

$insertResult2 = Invoke-PostgreSQLCommand -Command $insertSQL2 -Description "Insertando parámetro CALCULO_VALOR_POR_DIA_VIATICO"

$insertSQL3 = "INSERT INTO aocr_tbparametro (codigoparametro, valorparametro, descripcionparametro) VALUES ('CALCULO_PORCENTAJE_GASTOS_ADMIN', 8.00, 'Porcentaje de gastos administrativos (como porcentaje, ej: 8 para 8%)') ON CONFLICT (codigoparametro) DO UPDATE SET valorparametro = EXCLUDED.valorparametro, descripcionparametro = EXCLUDED.descripcionparametro;"

$insertResult3 = Invoke-PostgreSQLCommand -Command $insertSQL3 -Description "Insertando parámetro CALCULO_PORCENTAJE_GASTOS_ADMIN"

Write-Host "`n=== VERIFICANDO PARÁMETROS INSERTADOS ===" -ForegroundColor Yellow

$parametersCheckCmd = "SELECT codigoparametro, valorparametro, descripcionparametro FROM aocr_tbparametro WHERE codigoparametro IN ('CALCULO_VALOR_POR_ESTACION', 'CALCULO_VALOR_POR_DIA_VIATICO', 'CALCULO_PORCENTAJE_GASTOS_ADMIN') ORDER BY codigoparametro;"
$parametersCheck = Invoke-PostgreSQLCommand -Command $parametersCheckCmd -Description "Verificando parámetros insertados"

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