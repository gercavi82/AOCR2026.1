# Script para consultar tarifas en PostgreSQL usando Npgsql
# Base de datos: dgac_des (PostgreSQL 18)

# Buscar la DLL de Npgsql en el proyecto
$npgsqlPath = Get-ChildItem -Path ".\packages" -Filter "Npgsql.dll" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1

if ($npgsqlPath) {
    Write-Host "Encontrado Npgsql en: $($npgsqlPath.FullName)" -ForegroundColor Green
    Add-Type -Path $npgsqlPath.FullName
} else {
    Write-Host "No se encontro Npgsql.dll en packages" -ForegroundColor Red
    exit 1
}

# Cadena de conexión
$connectionString = "Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=control;Timeout=15;"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "CONSULTANDO TARIFAS EN BASE DE DATOS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

try {
    $conn = New-Object Npgsql.NpgsqlConnection($connectionString)
    $conn.Open()
    Write-Host "Conexion exitosa a PostgreSQL" -ForegroundColor Green
    Write-Host "  Servidor: 172.20.16.55" -ForegroundColor Gray
    Write-Host "  Base de datos: dgac_des" -ForegroundColor Gray
    Write-Host ""

    # Consulta: Ver tarifas problemáticas
    $query = "SELECT clave, valor, activo, updatedat, updatedby FROM aocr_tbparametro WHERE clave IN ('TARIFA_EMI_AOCR','TARIFA_REN_AOCR','TARIFA_MOD_AOCR_INC','TARIFA_MOD_AOCR_SIN_INC','TARIFA_INSPECCION_EXT','TARIFA_VIATICOS_INSPECTOR','PORCENTAJE_ADMIN_VIATICOS') AND deletedat IS NULL ORDER BY clave;"

    $cmd = New-Object Npgsql.NpgsqlCommand($query, $conn)
    $reader = $cmd.ExecuteReader()
    
    Write-Host "VALORES ACTUALES DE TARIFAS:" -ForegroundColor Yellow
    Write-Host ("=" * 120) -ForegroundColor Yellow
    Write-Host ("{0,-35} {1,-25} {2,-10} {3,-25} {4,-20}" -f "CLAVE", "VALOR", "ACTIVO", "UPDATEDAT", "UPDATEDBY") -ForegroundColor White
    Write-Host ("=" * 120) -ForegroundColor Yellow

    $count = 0
    while ($reader.Read()) {
        $clave = $reader.GetString(0)
        $valor = $reader.GetString(1)
        $activo = $reader.GetBoolean(2)
        $updatedat = if ($reader.IsDBNull(3)) { "NULL" } else { $reader.GetDateTime(3).ToString("yyyy-MM-dd HH:mm") }
        $updatedby = if ($reader.IsDBNull(4)) { "NULL" } else { $reader.GetString(4) }
        
        # Detectar problemas en el valor
        $color = "Green"
        if ($valor -match '[\$,\s_]|USD') {
            $color = "Red"
        } elseif ($valor -match ',') {
            $color = "Yellow"
        }
        
        Write-Host ("{0,-35} {1,-25} {2,-10} {3,-25} {4,-20}" -f $clave, $valor, $activo, $updatedat, $updatedby) -ForegroundColor $color
        $count++
    }
    $reader.Close()

    if ($count -eq 0) {
        Write-Host ""
        Write-Host "NO SE ENCONTRARON REGISTROS" -ForegroundColor Red
        Write-Host "Los parametros pueden no existir en la base de datos." -ForegroundColor Yellow
        Write-Host ""
    } else {
        Write-Host ""
        Write-Host "Total de tarifas encontradas: $count" -ForegroundColor Green
        Write-Host ""
    }

    # Consulta 2: Ver TODOS los parámetros
    Write-Host ""
    Write-Host "TODOS LOS PARAMETROS ACTIVOS (primeros 30):" -ForegroundColor Cyan
    Write-Host ("=" * 80) -ForegroundColor Cyan
    
    $queryAll = "SELECT clave, valor, activo FROM aocr_tbparametro WHERE deletedat IS NULL ORDER BY clave LIMIT 30;"
    $cmdAll = New-Object Npgsql.NpgsqlCommand($queryAll, $conn)
    $readerAll = $cmdAll.ExecuteReader()
    
    while ($readerAll.Read()) {
        $clave = $readerAll.GetString(0)
        $valor = $readerAll.GetString(1)
        $activo = $readerAll.GetBoolean(2)
        
        $statusColor = if ($activo) { "Green" } else { "Gray" }
        $status = if ($activo) { "ACTIVO" } else { "INACTIVO" }
        Write-Host ("{0,-50} {1,-20} [{2}]" -f $clave, $valor, $status) -ForegroundColor $statusColor
    }
    $readerAll.Close()

    $conn.Close()
    Write-Host ""
    Write-Host "Consulta completada exitosamente" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Stack trace:" -ForegroundColor Gray
    Write-Host $_.Exception.StackTrace -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

