# ================================================
# Script PowerShell para crear la tabla email_queue
# ================================================

$pgPath = "C:\Program Files\PostgreSQL\17\bin\psql.exe"
$host = "172.20.16.55"
$port = "5432"
$database = "dgac_des"
$username = "root"
$password = "control"
$sqlFile = "c:\AOCR\AOCR\AOCR05-01-2026\AOCR1\AOCR\scripts\create_email_queue_table.sql"

Write-Host "Creando tabla email_queue en PostgreSQL..." -ForegroundColor Cyan

# Establecer la contraseña como variable de entorno
$env:PGPASSWORD = $password

# Ejecutar el script SQL
& $pgPath -h $host -p $port -U $username -d $database -f $sqlFile

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Tabla email_queue creada exitosamente" -ForegroundColor Green
} else {
    Write-Host "✗ Error al crear la tabla" -ForegroundColor Red
}

# Limpiar la variable de entorno
Remove-Item Env:\PGPASSWORD
