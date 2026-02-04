param(
    [string]$ConnectionString = "Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=control;Timeout=30;"
)

# Script simple para agregar columna banco
$sqlCommands = @(
    "ALTER TABLE aocr_tbpago ADD COLUMN IF NOT EXISTS banco VARCHAR(255);",
    "UPDATE aocr_tbpago SET banco = 'NO_ESPECIFICADO' WHERE banco IS NULL;"
)

Write-Host "Agregando columna banco a tabla aocr_tbpago..." -ForegroundColor Green

try {
    # Intentar cargar Npgsql
    Add-Type -Path "C:\AOCR\AOCR\AOCR05-01-2026\AOCR1\AOCR\packages\Npgsql.8.0.3\lib\netstandard2.0\Npgsql.dll"
    
    # Crear conexion
    $conn = New-Object Npgsql.NpgsqlConnection($ConnectionString)
    $conn.Open()
    
    Write-Host "Conexion establecida" -ForegroundColor Green
    
    # Ejecutar cada comando
    foreach ($sql in $sqlCommands) {
        $cmd = New-Object Npgsql.NpgsqlCommand($sql, $conn)
        $result = $cmd.ExecuteNonQuery()
        Write-Host "Comando ejecutado: $result filas afectadas" -ForegroundColor White
    }
    
    # Verificar columna
    $checkSql = "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'aocr_tbpago' AND column_name = 'banco';"
    $checkCmd = New-Object Npgsql.NpgsqlCommand($checkSql, $conn)
    $count = $checkCmd.ExecuteScalar()
    
    if ($count -gt 0) {
        Write-Host "Columna banco creada exitosamente" -ForegroundColor Green
    } else {
        Write-Host "Error: Columna banco no fue creada" -ForegroundColor Red
    }
    
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    if ($conn -and $conn.State -eq "Open") {
        $conn.Close()
        Write-Host "Conexion cerrada" -ForegroundColor Blue
    }
}