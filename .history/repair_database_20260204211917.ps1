# ===================================================
# SCRIPT DE REPARACIÓN DE BASE DE DATOS AOCR
# Soluciona errores de columnas faltantes
# ===================================================

param(
    [string]$ConnectionString = $null
)

Write-Host "=== INICIANDO REPARACIÓN DE BASE DE DATOS AOCR ===" -ForegroundColor Yellow

# Función para ejecutar SQL y capturar errores
function Ejecutar-SQL {
    param($sql, $descripcion)
    
    try {
        Write-Host "⏳ $descripcion..." -ForegroundColor Cyan
        $result = $connectionObj.ExecuteNonQuery($sql)
        Write-Host "✅ $descripcion completado" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "❌ Error en $descripcion`: $_" -ForegroundColor Red
        return $false
    }
}

try {
    # 1. OBTENER CADENA DE CONEXIÓN
    if (-not $ConnectionString) {
        Write-Host "🔍 Buscando cadena de conexión en Web.config..." -ForegroundColor Cyan
        
        $webConfigPath = "C:\AOCR\AOCR\AOCR05-01-2026\AOCR1\AOCR\AOCR\Web.config"
        if (Test-Path $webConfigPath) {
            [xml]$webConfig = Get-Content $webConfigPath
            $connString = $webConfig.configuration.connectionStrings.add | Where-Object { $_.name -eq "AOCRConnection" }
            if ($connString) {
                $ConnectionString = $connString.connectionString
                Write-Host "✅ Cadena de conexión encontrada" -ForegroundColor Green
            } else {
                throw "No se encontró la conexión 'AOCRConnection' en Web.config"
            }
        } else {
            throw "No se encontró el archivo Web.config"
        }
    }

    # 2. CARGAR ASSEMBLY NPGSQL
    Write-Host "📚 Cargando Npgsql..." -ForegroundColor Cyan
    try {
        # Intentar cargar desde GAC o carpeta bin
        $npgsqlPath = "C:\AOCR\AOCR\AOCR05-01-2026\AOCR1\AOCR\packages\Npgsql.6.0.11\lib\net6.0\Npgsql.dll"
        if (Test-Path $npgsqlPath) {
            Add-Type -Path $npgsqlPath
        } else {
            # Alternativa: usar el assembly cargado
            [System.Reflection.Assembly]::LoadWithPartialName("Npgsql") | Out-Null
        }
        Write-Host "✅ Npgsql cargado correctamente" -ForegroundColor Green
    }
    catch {
        Write-Host "⚠️  Advertencia: No se pudo cargar Npgsql desde archivo, usando versión del sistema" -ForegroundColor Yellow
    }

    # 3. CONECTAR A POSTGRESQL
    Write-Host "🔗 Conectando a PostgreSQL..." -ForegroundColor Cyan
    $connectionObj = New-Object Npgsql.NpgsqlConnection($ConnectionString)
    $connectionObj.Open()
    Write-Host "✅ Conexión establecida" -ForegroundColor Green

    # 4. LEER SCRIPT SQL DE REPARACIÓN
    $sqlFilePath = "C:\AOCR\AOCR\AOCR05-01-2026\AOCR1\AOCR\fix_database_complete.sql"
    if (-not (Test-Path $sqlFilePath)) {
        throw "No se encontró el archivo SQL de reparación: $sqlFilePath"
    }
    
    $sqlContent = Get-Content $sqlFilePath -Raw
    Write-Host "✅ Script SQL cargado" -ForegroundColor Green

    # 5. EJECUTAR REPARACIONES EN ORDEN
    Write-Host "`n🔧 EJECUTANDO REPARACIONES..." -ForegroundColor Yellow
    
    # Dividir el SQL en comandos individuales
    $comandos = $sqlContent -split "(?<=;)\s*\n" | Where-Object { $_.Trim() -ne "" -and -not $_.Trim().StartsWith("--") }
    
    $exitos = 0
    $errores = 0
    
    foreach ($comando in $comandos) {
        $comandoLimpio = $comando.Trim()
        if ($comandoLimpio -and -not $comandoLimpio.StartsWith("--")) {
            $cmd = New-Object Npgsql.NpgsqlCommand($comandoLimpio, $connectionObj)
            try {
                $result = $cmd.ExecuteNonQuery()
                $exitos++
                
                # Mostrar resultado específico
                if ($comandoLimpio.StartsWith("CREATE TABLE")) {
                    Write-Host "   ✅ Tabla creada/verificada" -ForegroundColor Green
                } elseif ($comandoLimpio.StartsWith("ALTER TABLE")) {
                    Write-Host "   ✅ Columna agregada/verificada" -ForegroundColor Green
                } elseif ($comandoLimpio.StartsWith("INSERT INTO")) {
                    Write-Host "   ✅ Parámetros configurables insertados" -ForegroundColor Green
                } elseif ($comandoLimpio.StartsWith("UPDATE")) {
                    Write-Host "   ✅ Registros actualizados" -ForegroundColor Green
                } elseif ($comandoLimpio.StartsWith("CREATE INDEX")) {
                    Write-Host "   ✅ Índice creado" -ForegroundColor Green
                }
            }
            catch {
                $errores++
                if ($_.Exception.Message -notmatch "already exists|duplicate key") {
                    Write-Host "   ⚠️  $($_.Exception.Message)" -ForegroundColor Yellow
                }
            }
        }
    }

    # 6. VERIFICACIÓN FINAL
    Write-Host "`n🔍 VERIFICACIÓN FINAL..." -ForegroundColor Yellow
    
    try {
        $verifyCmd = New-Object Npgsql.NpgsqlCommand(@"
            SELECT 'TABLA PARÁMETROS' as tipo, 
                   COUNT(*) as total_registros,
                   COUNT(CASE WHEN activo = TRUE THEN 1 END) as activos
            FROM aocr_tbparametro
            UNION ALL
            SELECT 'PAGOS CON BANCO' as tipo,
                   COUNT(*) as total_registros,
                   COUNT(CASE WHEN banco IS NOT NULL AND banco != '' THEN 1 END) as con_banco
            FROM aocr_tbpago;
        ", $connectionObj)
        
        $reader = $verifyCmd.ExecuteReader()
        Write-Host "`n📊 RESUMEN DE DATOS:" -ForegroundColor Cyan
        while ($reader.Read()) {
            $tipo = $reader["tipo"]
            $total = $reader["total_registros"]
            $segunda_col = $reader[2]
            Write-Host "   $tipo`: $total total, $segunda_col configurados" -ForegroundColor White
        }
        $reader.Close()
    }
    catch {
        Write-Host "⚠️  No se pudo ejecutar verificación final: $_" -ForegroundColor Yellow
    }

    # 7. RESULTADO FINAL
    Write-Host "`n🎉 REPARACIÓN COMPLETADA" -ForegroundColor Green
    Write-Host "   ✅ Comandos ejecutados exitosamente: $exitos" -ForegroundColor Green
    Write-Host "   ⚠️  Errores/Advertencias: $errores" -ForegroundColor Yellow
    Write-Host "`n📋 PRÓXIMOS PASOS:" -ForegroundColor Cyan
    Write-Host "   1. Reiniciar la aplicación web (IIS Express)" -ForegroundColor White
    Write-Host "   2. Probar la funcionalidad de parámetros configurables" -ForegroundColor White
    Write-Host "   3. Verificar que los errores de 'codigoparametro' hayan desaparecido" -ForegroundColor White
    
}
catch {
    Write-Host "`n❌ ERROR CRÍTICO: $_" -ForegroundColor Red
    Write-Host "Revise la cadena de conexión y que PostgreSQL esté ejecutándose" -ForegroundColor Yellow
}
finally {
    if ($connectionObj -and $connectionObj.State -eq "Open") {
        $connectionObj.Close()
        Write-Host "🔐 Conexión cerrada" -ForegroundColor Gray
    }
}

Write-Host "`n=== REPARACIÓN FINALIZADA ===" -ForegroundColor Yellow
Read-Host "Presione Enter para continuar"