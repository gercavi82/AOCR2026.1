$connectionString = "Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=control;Timeout=15;"
$scriptPath = "c:\proyectos\AOCR\scripts\db\20260922_add_usuario_id_to_auditoria.sql"

# Leer el script SQL
$sql = Get-Content -Path $scriptPath -Raw

# Cargar ensamblado de Npgsql desde el proyecto
$npgsqlPath = "C:\proyectos\AOCR\packages\Npgsql.4.1.13\lib\net461\Npgsql.dll"

if (-not (Test-Path $npgsqlPath)) {
    Write-Host "ERROR: No se encontró Npgsql.dll en $npgsqlPath"
    exit 1
}

[Reflection.Assembly]::LoadFrom($npgsqlPath) | Out-Null
Write-Host "Npgsql.dll cargado: $npgsqlPath"

try {
    # Crear conexión y ejecutar SQL
    $connection = New-Object Npgsql.NpgsqlConnection $connectionString
    $connection.Open()
    Write-Host "Conexión abierta exitosamente"
    
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.ExecuteNonQuery()
    
    Write-Host "Script SQL ejecutado exitosamente"
    $connection.Close()
    
} catch {
    Write-Host "ERROR: $_"
    exit 1
}
