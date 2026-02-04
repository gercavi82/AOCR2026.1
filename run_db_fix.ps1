# Script de PowerShell para reparar base de datos AOCR
$ErrorActionPreference = "Stop"

Write-Host "=== REPARANDO BASE DE DATOS AOCR ===" -ForegroundColor Green

# Leer el script SQL
$scriptSQL = Get-Content "C:\AOCR\AOCR\AOCR05-01-2026\AOCR1\AOCR\fix_database_issues.sql" -Raw

# Parámetros de conexión  
$server = "172.20.16.55"
$port = "5432"
$database = "dgac_des"  
$username = "root"
$password = "control"

Write-Host "Conectando a: $server`:$port/$database" -ForegroundColor Yellow

try {
    # Usar psql directamente
    $psqlPath = "C:\Program Files\PostgreSQL\18\bin\psql.exe"
    $env:PGPASSWORD = $password
    
    # Ejecutar el script
    Write-Host "Ejecutando script..." -ForegroundColor Yellow
    
    $scriptSQL | & $psqlPath -h $server -p $port -d $database -U $username -q
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "SUCCESS: Script ejecutado correctamente" -ForegroundColor Green
    } else {
        Write-Host "ERROR: Codigo de salida $LASTEXITCODE" -ForegroundColor Red
    }
    
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

Write-Host "=== PROCESO COMPLETADO ===" -ForegroundColor Green