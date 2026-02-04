## Script de PowerShell para ejecutar fix_database_issues.sql
Add-Type -AssemblyName "System.Data"

# Parámetros de conexión
$connectionString = "Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=control;Timeout=30;"

# Leer el script SQL
$scriptPath = "C:\AOCR\AOCR\AOCR05-01-2026\AOCR1\AOCR\fix_database_issues.sql"
$sqlScript = Get-Content -Path $scriptPath -Raw -Encoding UTF8

Write-Host "=== EJECUTANDO SCRIPT DE REPARACIÓN DE BASE DE DATOS ===" -ForegroundColor Green
Write-Host "Host: 172.20.16.55" -ForegroundColor Yellow
Write-Host "Base de datos: dgac_des" -ForegroundColor Yellow
Write-Host "Script: $scriptPath" -ForegroundColor Yellow
Write-Host ""

try {
    # Cargar Npgsql si está disponible
    Add-Type -Path "C:\AOCR\AOCR\AOCR05-01-2026\AOCR1\AOCR\packages\Npgsql.8.0.3\lib\netstandard2.0\Npgsql.dll" -ErrorAction SilentlyContinue
    
    # Crear conexión
    $connection = New-Object Npgsql.NpgsqlConnection($connectionString)
    $connection.Open()
    
    Write-Host "✅ Conexión establecida correctamente" -ForegroundColor Green
    
    # Ejecutar el script
    $command = New-Object Npgsql.NpgsqlCommand($sqlScript, $connection)
    $command.CommandTimeout = 120
    
    Write-Host "🔄 Ejecutando script..." -ForegroundColor Yellow
    $result = $command.ExecuteNonQuery()
    
    Write-Host "✅ Script ejecutado exitosamente" -ForegroundColor Green
    Write-Host "Filas afectadas: $result" -ForegroundColor White
    
} catch {
    Write-Host "❌ Error al ejecutar el script:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Detalles del error:" -ForegroundColor Yellow
    Write-Host $_.Exception.ToString() -ForegroundColor Gray
    
} finally {
    if ($connection -and $connection.State -eq "Open") {
        $connection.Close()
        Write-Host "🔒 Conexión cerrada" -ForegroundColor Blue
    }
}

Write-Host ""
Write-Host "=== SCRIPT DE REPARACIÓN COMPLETADO ===" -ForegroundColor Green