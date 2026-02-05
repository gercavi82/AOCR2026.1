# Script simplificado para corregir la base de datos

Write-Host "=== CORRECCIÓN DE TABLA AOCR_TBPARAMETRO ===" -ForegroundColor Cyan

# Solicitar información de conexión
Write-Host "Proporciona la información de conexión:" -ForegroundColor Yellow

$server = Read-Host "Servidor PostgreSQL (localhost por defecto)"
if ([string]::IsNullOrWhiteSpace($server)) { $server = "localhost" }

$database = Read-Host "Nombre de la base de datos"
if ([string]::IsNullOrWhiteSpace($database)) { 
    Write-Host "ERROR: Debe proporcionar el nombre de la base de datos" -ForegroundColor Red
    Read-Host "Presiona Enter para salir"
    exit
}

$username = Read-Host "Usuario (postgres por defecto)"
if ([string]::IsNullOrWhiteSpace($username)) { $username = "postgres" }

$port = Read-Host "Puerto (5432 por defecto)"
if ([string]::IsNullOrWhiteSpace($port)) { $port = "5432" }

# Buscar psql
$psqlPaths = @(
    "C:\Program Files\PostgreSQL\16\bin\psql.exe",
    "C:\Program Files\PostgreSQL\15\bin\psql.exe",
    "C:\Program Files\PostgreSQL\14\bin\psql.exe",
    "C:\Program Files\PostgreSQL\13\bin\psql.exe"
)

$psqlPath = $null
foreach ($path in $psqlPaths) {
    if (Test-Path $path) {
        $psqlPath = $path
        break
    }
}

if ($psqlPath -eq $null) {
    Write-Host "No se encontró PostgreSQL. Proporciona la ruta completa a psql.exe:" -ForegroundColor Red
    $psqlPath = Read-Host "Ruta a psql.exe"
    if (!(Test-Path $psqlPath)) {
        Write-Host "Ruta no válida" -ForegroundColor Red
        Read-Host "Presiona Enter para salir"
        exit
    }
}

# Solicitar contraseña
$password = Read-Host "Contraseña" -AsSecureString
$plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))

Write-Host "Usando psql: $psqlPath" -ForegroundColor Green

# Crear archivo SQL temporal
$sqlContent = @"
-- Verificar y agregar columnas necesarias
ALTER TABLE aocr_tbparametro ADD COLUMN IF NOT EXISTS codigoparametro VARCHAR(100) UNIQUE;
ALTER TABLE aocr_tbparametro ADD COLUMN IF NOT EXISTS valorparametro DECIMAL(10,2);
ALTER TABLE aocr_tbparametro ADD COLUMN IF NOT EXISTS descripcionparametro VARCHAR(255);

-- Insertar parámetros necesarios
INSERT INTO aocr_tbparametro (codigoparametro, valorparametro, descripcionparametro) 
VALUES ('CALCULO_VALOR_POR_ESTACION', 500.00, 'Valor por estación para cálculo de inspecciones')
ON CONFLICT (codigoparametro) DO UPDATE SET 
    valorparametro = EXCLUDED.valorparametro,
    descripcionparametro = EXCLUDED.descripcionparametro;

INSERT INTO aocr_tbparametro (codigoparametro, valorparametro, descripcionparametro) 
VALUES ('CALCULO_VALOR_POR_DIA_VIATICO', 80.00, 'Valor por día de viático para inspectores')
ON CONFLICT (codigoparametro) DO UPDATE SET 
    valorparametro = EXCLUDED.valorparametro,
    descripcionparametro = EXCLUDED.descripcionparametro;

INSERT INTO aocr_tbparametro (codigoparametro, valorparametro, descripcionparametro) 
VALUES ('CALCULO_PORCENTAJE_GASTOS_ADMIN', 8.00, 'Porcentaje de gastos administrativos (como porcentaje, ej: 8 para 8%)')
ON CONFLICT (codigoparametro) DO UPDATE SET 
    valorparametro = EXCLUDED.valorparametro,
    descripcionparametro = EXCLUDED.descripcionparametro;

-- Verificar resultados
SELECT 'Parámetros insertados:' as resultado;
SELECT codigoparametro, valorparametro, descripcionparametro 
FROM aocr_tbparametro 
WHERE codigoparametro IN ('CALCULO_VALOR_POR_ESTACION', 'CALCULO_VALOR_POR_DIA_VIATICO', 'CALCULO_PORCENTAJE_GASTOS_ADMIN')
ORDER BY codigoparametro;
"@

$tempSqlFile = "temp_fix_parametros.sql"
$sqlContent | Out-File -FilePath $tempSqlFile -Encoding UTF8

Write-Host "`nEjecutando correcciones en la base de datos..." -ForegroundColor Yellow

# Ejecutar SQL
$env:PGPASSWORD = $plainPassword
try {
    & $psqlPath -h $server -p $port -U $username -d $database -f $tempSqlFile
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`n✓ Correcciones aplicadas exitosamente!" -ForegroundColor Green
        Write-Host "`n📌 SIGUIENTE PASO:" -ForegroundColor Yellow
        Write-Host "   Reinicia la aplicación web (detén y vuelve a ejecutar desde Visual Studio)" -ForegroundColor White
        Write-Host "   Los errores de 'codigoparametro does not exist' deberían desaparecer." -ForegroundColor White
    } else {
        Write-Host "`n✗ Hubo errores ejecutando el script" -ForegroundColor Red
    }
} finally {
    # Limpiar archivo temporal
    if (Test-Path $tempSqlFile) {
        Remove-Item $tempSqlFile
    }
    # Limpiar variable de entorno
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}

Write-Host "`nPresiona Enter para continuar..." -ForegroundColor Gray
Read-Host