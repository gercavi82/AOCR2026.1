# Script de PowerShell para agregar la columna banco a la tabla aocr_tbpago
Add-Type -AssemblyName "System.Data"

# Parámetros de conexión
$connectionString = "Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=control;Timeout=30;"

# Script SQL para agregar la columna banco
$sqlScript = @"
-- Script para agregar columna banco a la tabla de pagos
ALTER TABLE aocr_tbpago ADD COLUMN IF NOT EXISTS banco VARCHAR(255);

-- Actualizar registros existentes con valor por defecto
UPDATE aocr_tbpago SET banco = 'NO_ESPECIFICADO' WHERE banco IS NULL;

-- Verificar que la columna fue creada
SELECT COUNT(*) FROM information_schema.columns 
WHERE table_name = 'aocr_tbpago' AND column_name = 'banco';
"@

Write-Host "=== AGREGANDO COLUMNA BANCO A TABLA DE PAGOS ===" -ForegroundColor Green
Write-Host "Host: 172.20.16.55" -ForegroundColor Yellow
Write-Host "Base de datos: dgac_des" -ForegroundColor Yellow
Write-Host ""

try {
    # Cargar Npgsql si está disponible
    try {
        Add-Type -Path "C:\AOCR\AOCR\AOCR05-01-2026\AOCR1\AOCR\packages\Npgsql.8.0.3\lib\netstandard2.0\Npgsql.dll" -ErrorAction SilentlyContinue
    } catch {
        Write-Host "⚠️  No se pudo cargar Npgsql desde el path especificado, intentando con GAC..." -ForegroundColor Yellow
    }
    
    # Crear conexión
    $connection = New-Object Npgsql.NpgsqlConnection($connectionString)
    $connection.Open()
    
    Write-Host "✅ Conexión establecida correctamente" -ForegroundColor Green
    
    # Ejecutar el script
    $command = New-Object Npgsql.NpgsqlCommand($sqlScript, $connection)
    $command.CommandTimeout = 120
    
    Write-Host "🔄 Ejecutando script..." -ForegroundColor Yellow
    $result = $command.ExecuteScalar()
    
    Write-Host "✅ Script ejecutado exitosamente" -ForegroundColor Green
    Write-Host "Columnas 'banco' encontradas: $result" -ForegroundColor White
    
    if ($result -gt 0) {
        Write-Host "✅ La columna 'banco' existe en la tabla aocr_tbpago" -ForegroundColor Green
    } else {
        Write-Host "❌ La columna 'banco' NO fue creada correctamente" -ForegroundColor Red
    }
    
} catch {
    Write-Host "❌ Error al ejecutar el script:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Detalles del error:" -ForegroundColor Yellow
    Write-Host $_.Exception.ToString() -ForegroundColor Gray
    
    # Si falla Npgsql, intentar con método alternativo
    Write-Host ""
    Write-Host "Intentando método alternativo con psql..." -ForegroundColor Yellow
    
} finally {
    if ($connection -and $connection.State -eq "Open") {
        $connection.Close()
        Write-Host "🔒 Conexión cerrada" -ForegroundColor Blue
    }
}

Write-Host ""
Write-Host "=== PROCESO COMPLETADO ===" -ForegroundColor Green