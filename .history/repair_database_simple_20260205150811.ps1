# Script simplificado de reparación de base de datos
Write-Host "=== REPARACIÓN DE BASE DE DATOS AOCR ===" -ForegroundColor Yellow

try {
    # 1. Obtener cadena de conexión
    $webConfigPath = "C:\AOCR\AOCR\AOCR05-01-2026\AOCR1\AOCR\AOCR\Web.config"
    if (Test-Path $webConfigPath) {
        [xml]$webConfig = Get-Content $webConfigPath
        $connStringNode = $webConfig.configuration.connectionStrings.add | Where-Object { $_.name -eq "AOCRConnection" }
        $ConnectionString = $connStringNode.connectionString
        Write-Host "✅ Cadena de conexión encontrada" -ForegroundColor Green
    } else {
        throw "No se encontró Web.config"
    }

    # 2. Cargar Npgsql
    Write-Host "📚 Cargando Npgsql..." -ForegroundColor Cyan
    Add-Type -AssemblyName "Npgsql"

    # 3. Conectar
    Write-Host "🔗 Conectando a PostgreSQL..." -ForegroundColor Cyan
    $conn = New-Object Npgsql.NpgsqlConnection($ConnectionString)
    $conn.Open()
    Write-Host "✅ Conexión establecida" -ForegroundColor Green

    # 4. Crear tabla parámetros
    Write-Host "🔧 Creando tabla de parámetros..." -ForegroundColor Cyan
    $sql1 = @"
CREATE TABLE IF NOT EXISTS aocr_tbparametro (
    codigoparametro SERIAL PRIMARY KEY,
    clave VARCHAR(100) NOT NULL UNIQUE,
    valor VARCHAR(500) NOT NULL,
    descripcion VARCHAR(1000),
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    createdat TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    createdby INTEGER,
    updatedat TIMESTAMP,
    updatedby INTEGER,
    deletedat TIMESTAMP,
    deletedby INTEGER
);
"@
    
    $cmd1 = New-Object Npgsql.NpgsqlCommand($sql1, $conn)
    $cmd1.ExecuteNonQuery() | Out-Null
    Write-Host "✅ Tabla de parámetros creada" -ForegroundColor Green

    # 5. Agregar columna banco
    Write-Host "🔧 Agregando columna banco..." -ForegroundColor Cyan
    $sql2 = "ALTER TABLE aocr_tbpago ADD COLUMN IF NOT EXISTS banco VARCHAR(255);"
    $cmd2 = New-Object Npgsql.NpgsqlCommand($sql2, $conn)
    $cmd2.ExecuteNonQuery() | Out-Null
    Write-Host "✅ Columna banco agregada" -ForegroundColor Green

    # 6. Insertar parámetros básicos
    Write-Host "🔧 Insertando parámetros configurables..." -ForegroundColor Cyan
    $parametros = @(
        @{clave="TEST_EMPRESA_NOMBRE"; valor="AERONÁUTICA CIVIL"; descripcion="Nombre de empresa para testing"},
        @{clave="DEMO_MONTO_FIJO"; valor="80.00"; descripcion="Monto fijo para demostraciones"},
        @{clave="TARIFA_EMI_AOCR"; valor="250.00"; descripcion="Tarifa emisión AOCR"},
        @{clave="TARIFA_REN_AOCR"; valor="200.00"; descripcion="Tarifa renovación AOCR"},
        @{clave="PORCENTAJE_ADMIN_VIATICOS"; valor="15"; descripcion="Porcentaje administrativo"}
    )
    
    foreach ($param in $parametros) {
        $sqlInsert = "INSERT INTO aocr_tbparametro (clave, valor, descripcion, activo, createdat, createdby) VALUES (@clave, @valor, @desc, TRUE, NOW(), 1) ON CONFLICT (clave) DO UPDATE SET valor = EXCLUDED.valor, updatedat = NOW();"
        $cmdInsert = New-Object Npgsql.NpgsqlCommand($sqlInsert, $conn)
        $cmdInsert.Parameters.AddWithValue("@clave", $param.clave) | Out-Null
        $cmdInsert.Parameters.AddWithValue("@valor", $param.valor) | Out-Null
        $cmdInsert.Parameters.AddWithValue("@desc", $param.descripcion) | Out-Null
        $cmdInsert.ExecuteNonQuery() | Out-Null
    }
    Write-Host "✅ Parámetros insertados" -ForegroundColor Green

    # 7. Actualizar registros de pagos
    Write-Host "🔧 Actualizando registros de pagos..." -ForegroundColor Cyan
    $sql3 = "UPDATE aocr_tbpago SET banco = 'NO_ESPECIFICADO' WHERE banco IS NULL OR banco = '';"
    $cmd3 = New-Object Npgsql.NpgsqlCommand($sql3, $conn)
    $result = $cmd3.ExecuteNonQuery()
    Write-Host "✅ $result registros de pagos actualizados" -ForegroundColor Green

    # 8. Verificación
    Write-Host "🔍 Verificando resultados..." -ForegroundColor Cyan
    $sqlVerify = "SELECT COUNT(*) FROM aocr_tbparametro WHERE activo = TRUE;"
    $cmdVerify = New-Object Npgsql.NpgsqlCommand($sqlVerify, $conn)
    $count = $cmdVerify.ExecuteScalar()
    Write-Host "✅ $count parámetros activos en la base de datos" -ForegroundColor Green

    Write-Host "`n🎉 REPARACIÓN COMPLETADA EXITOSAMENTE" -ForegroundColor Green
    Write-Host "Reinicie la aplicación web para aplicar los cambios" -ForegroundColor Yellow

} catch {
    Write-Host "❌ ERROR: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    if ($conn -and $conn.State -eq "Open") {
        $conn.Close()
    }
}

Read-Host "Presione Enter para continuar"