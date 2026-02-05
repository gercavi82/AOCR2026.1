# Script para verificar las tablas relacionadas con órdenes en PostgreSQL
param(
    [string]$Server = "172.20.16.55",
    [int]$Port = 5432,
    [string]$Database = "dgac_des",
    [string]$Username = "postgres"
)

# Solicitar contraseña de forma segura
$Password = Read-Host "Ingrese la contraseña para $Username" -AsSecureString
$plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password))

Write-Host "Conectando a PostgreSQL..." -ForegroundColor Yellow

# String de conexión
$connectionString = "Host=$Server;Port=$Port;Database=$Database;Username=$Username;Password=$plainPassword;SSL Mode=Prefer;"

try {
    # Cargar el ensamblado Npgsql
    Add-Type -Path "C:\AOCR\AOCR\AOCR05-01-2026\AOCR1\AOCR\packages\Npgsql.5.0.18\lib\net5.0\Npgsql.dll"

    # Crear conexión
    $conn = New-Object Npgsql.NpgsqlConnection($connectionString)
    $conn.Open()
    
    Write-Host "✅ Conexión establecida exitosamente" -ForegroundColor Green
    
    # Consultar tablas relacionadas con órdenes
    Write-Host "`n🔍 Verificando tablas relacionadas con órdenes:" -ForegroundColor Cyan
    
    $query = @"
SELECT table_name, table_comment 
FROM information_schema.tables 
WHERE table_schema = 'public' 
AND table_name LIKE '%orden%'
ORDER BY table_name;
"@
    
    $cmd = New-Object Npgsql.NpgsqlCommand($query, $conn)
    $reader = $cmd.ExecuteReader()
    
    $tables = @()
    while ($reader.Read()) {
        $tables += [PSCustomObject]@{
            TableName = $reader["table_name"]
            Comment = $reader["table_comment"]
        }
    }
    $reader.Close()
    
    if ($tables.Count -eq 0) {
        Write-Host "❌ No se encontraron tablas relacionadas con órdenes" -ForegroundColor Red
    } else {
        Write-Host "📋 Tablas encontradas:" -ForegroundColor Green
        $tables | Format-Table -AutoSize
    }
    
    # Verificar específicamente las dos tablas sospechosas
    Write-Host "`n🔍 Verificando estructura de tablas específicas:" -ForegroundColor Cyan
    
    $tablesToCheck = @("aocr_or_orden", "aocr_orden_recaudacion")
    
    foreach ($tableName in $tablesToCheck) {
        Write-Host "`n--- Verificando tabla: $tableName ---" -ForegroundColor Yellow
        
        # Verificar si existe la tabla
        $existQuery = "SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = '$tableName');"
        $cmd = New-Object Npgsql.NpgsqlCommand($existQuery, $conn)
        $exists = $cmd.ExecuteScalar()
        
        if ($exists) {
            Write-Host "✅ La tabla $tableName EXISTE" -ForegroundColor Green
            
            # Obtener estructura de la tabla
            $structQuery = @"
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_name = '$tableName'
ORDER BY ordinal_position;
"@
            
            $cmd = New-Object Npgsql.NpgsqlCommand($structQuery, $conn)
            $reader = $cmd.ExecuteReader()
            
            $columns = @()
            while ($reader.Read()) {
                $columns += [PSCustomObject]@{
                    Column = $reader["column_name"]
                    Type = $reader["data_type"]
                    Nullable = $reader["is_nullable"]
                    Default = $reader["column_default"]
                }
            }
            $reader.Close()
            
            Write-Host "Columnas de $tableName :" -ForegroundColor Cyan
            $columns | Format-Table -AutoSize
            
            # Contar registros
            $countQuery = "SELECT COUNT(*) FROM $tableName;"
            $cmd = New-Object Npgsql.NpgsqlCommand($countQuery, $conn)
            $count = $cmd.ExecuteScalar()
            Write-Host "📊 Total de registros en $tableName : $count" -ForegroundColor White
            
        } else {
            Write-Host "❌ La tabla $tableName NO EXISTE" -ForegroundColor Red
        }
    }
    
    # Verificar relación con solicitudes
    Write-Host "`n🔍 Verificando relación con solicitudes:" -ForegroundColor Cyan
    
    $relationQuery = @"
SELECT 
    t.table_name,
    c.column_name,
    c.data_type
FROM information_schema.tables t
JOIN information_schema.columns c ON t.table_name = c.table_name
WHERE t.table_schema = 'public'
AND c.column_name LIKE '%solicitud%'
AND t.table_name LIKE '%orden%'
ORDER BY t.table_name, c.column_name;
"@
    
    $cmd = New-Object Npgsql.NpgsqlCommand($relationQuery, $conn)
    $reader = $cmd.ExecuteReader()
    
    $relations = @()
    while ($reader.Read()) {
        $relations += [PSCustomObject]@{
            Table = $reader["table_name"]
            Column = $reader["column_name"]
            Type = $reader["data_type"]
        }
    }
    $reader.Close()
    
    if ($relations.Count -gt 0) {
        Write-Host "🔗 Columnas relacionadas con solicitudes:" -ForegroundColor Green
        $relations | Format-Table -AutoSize
    }
    
    $conn.Close()
    Write-Host "`n✅ Verificación completada exitosamente" -ForegroundColor Green
    
} catch {
    Write-Host "❌ Error durante la verificación: $($_.Exception.Message)" -ForegroundColor Red
    if ($conn -and $conn.State -eq "Open") {
        $conn.Close()
    }
}

# Limpiar la contraseña de la memoria
$plainPassword = $null