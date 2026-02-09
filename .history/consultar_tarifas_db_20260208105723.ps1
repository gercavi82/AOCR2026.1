# Script para consultar tarifas en PostgreSQL usando Npgsql
# Base de datos: dgac_des (PostgreSQL 18)

# Buscar la DLL de Npgsql en el proyecto
$npgsqlPath = Get-ChildItem -Path ".\packages" -Filter "Npgsql.dll" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1

if ($npgsqlPath) {
    Write-Host "✓ Encontrado Npgsql en: $($npgsqlPath.FullName)" -ForegroundColor Green
    Add-Type -Path $npgsqlPath.FullName
} else {
    Write-Host "✗ No se encontró Npgsql.dll en packages" -ForegroundColor Red
    Write-Host "Intentando con versión del sistema..." -ForegroundColor Yellow
    try {
        Add-Type -Path "C:\Program Files\Npgsql\Npgsql.dll"
    } catch {
        Write-Host "✗ No se pudo cargar Npgsql. Asegúrese de que esté instalado." -ForegroundColor Red
        exit 1
    }
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
    Write-Host "✓ Conexión exitosa a PostgreSQL" -ForegroundColor Green
    Write-Host "  Servidor: 172.20.16.55" -ForegroundColor Gray
    Write-Host "  Base de datos: dgac_des`n" -ForegroundColor Gray

    # Consulta 1: Ver tarifas problemáticas
    $query = @"
SELECT 
    clave,
    valor,
    activo,
    updatedat,
    updatedby
FROM aocr_tbparametro
WHERE clave IN (
    'TARIFA_EMI_AOCR',
    'TARIFA_REN_AOCR',
    'TARIFA_MOD_AOCR_INC',
    'TARIFA_MOD_AOCR_SIN_INC',
    'TARIFA_INSPECCION_EXT',
    'TARIFA_VIATICOS_INSPECTOR',
    'PORCENTAJE_ADMIN_VIATICOS'
)
AND deletedat IS NULL
ORDER BY clave;
"@

    $cmd = New-Object Npgsql.NpgsqlCommand($query, $conn)
    $reader = $cmd.ExecuteReader()
    
    Write-Host "VALORES ACTUALES DE TARIFAS:" -ForegroundColor Yellow
    Write-Host ("=" * 120) -ForegroundColor Yellow
    Write-Host ("{0,-35} {1,-25} {2,-10} {3,-25} {4,-20}" -f "CLAVE", "VALOR", "ACTIVO", "UPDATEDAT", "UPDATEDBY") -ForegroundColor White
    Write-Host ("=" * 120) -ForegroundColor Yellow

    $count = 0
    while ($reader.Read()) {
        $clave = $reader["clave"]
        $valor = $reader["valor"]
        $activo = $reader["activo"]
        $updatedat = if ($reader["updatedat"] -is [DBNull]) { "NULL" } else { $reader["updatedat"].ToString("yyyy-MM-dd HH:mm") }
        $updatedby = if ($reader["updatedby"] -is [DBNull]) { "NULL" } else { $reader["updatedby"] }
        
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
        Write-Host "`n⚠ NO SE ENCONTRARON REGISTROS" -ForegroundColor Red
        Write-Host "Los parámetros pueden no existir en la base de datos.`n" -ForegroundColor Yellow
    } else {
        Write-Host "`n✓ Total de tarifas encontradas: $count`n" -ForegroundColor Green
    }

    # Consulta 2: Ver TODOS los parámetros
    Write-Host "`nTODOS LOS PARÁMETROS ACTIVOS:" -ForegroundColor Cyan
    Write-Host ("=" * 80) -ForegroundColor Cyan
    
    $queryAll = "SELECT clave, valor, activo FROM aocr_tbparametro WHERE deletedat IS NULL ORDER BY clave LIMIT 30;"
    $cmdAll = New-Object Npgsql.NpgsqlCommand($queryAll, $conn)
    $readerAll = $cmdAll.ExecuteReader()
    
    while ($readerAll.Read()) {
        $clave = $readerAll["clave"]
        $valor = $readerAll["valor"]
        $activo = $readerAll["activo"]
        
        $statusColor = if ($activo) { "Green" } else { "Gray" }
        Write-Host ("{0,-50} {1,-20} [{2}]" -f $clave, $valor, $(if ($activo) {"ACTIVO"} else {"INACTIVO"})) -ForegroundColor $statusColor
    }
    $readerAll.Close()

    $conn.Close()
    Write-Host "`n✓ Consulta completada exitosamente" -ForegroundColor Green

} catch {
    Write-Host "`n✗ ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "`nStack trace:" -ForegroundColor Gray
    Write-Host $_.Exception.StackTrace -ForegroundColor Gray
}

Write-Host "`n========================================`n" -ForegroundColor Cyan
