# Script para insertar parámetros de tarifas AOCR configurables
# Elimina los valores "quemados" del código reemplazándolos por parámetros

param(
    [string]$ConnectionString = $null,
    [string]$Server = "localhost",
    [string]$Database = "aocr_db", 
    [string]$Username = "postgres",
    [string]$Password = "",
    [switch]$Help
)

if ($Help) {
    Write-Host "Uso: .\insertar_parametros_tarifas.ps1 [parámetros]"
    Write-Host ""
    Write-Host "Parámetros:"
    Write-Host "  -ConnectionString    Cadena de conexión completa"
    Write-Host "  -Server             Servidor de BD (default: localhost)"
    Write-Host "  -Database           Base de datos (default: aocr_db)"  
    Write-Host "  -Username           Usuario (default: postgres)"
    Write-Host "  -Password           Contraseña"
    Write-Host "  -Help               Muestra esta ayuda"
    Write-Host ""
    Write-Host "Ejemplos:"
    Write-Host "  .\insertar_parametros_tarifas.ps1 -Password 'mi_password'"
    Write-Host "  .\insertar_parametros_tarifas.ps1 -Server 'servidor' -Database 'bd' -Username 'user' -Password 'pass'"
    exit 0
}

# Construir cadena de conexión si no se proporcionó
if ([string]::IsNullOrEmpty($ConnectionString)) {
    $ConnectionString = "Host=$Server;Database=$Database;Username=$Username"
    if (-not [string]::IsNullOrEmpty($Password)) {
        $ConnectionString += ";Password=$Password"
    }
}

Write-Host "🔧 Insertando parámetros de tarifas AOCR configurables..." -ForegroundColor Yellow

try {
    # Verificar si Npgsql está disponible
    try {
        Add-Type -Path "Npgsql.dll" -ErrorAction Stop
    }
    catch {
        Write-Host "❌ Error: Npgsql.dll no encontrado. Instalando desde NuGet..." -ForegroundColor Red
        
        # Intentar instalar Npgsql vía NuGet si está disponible
        if (Get-Command Install-Package -ErrorAction SilentlyContinue) {
            Install-Package Npgsql -Force
        } else {
            Write-Host "❌ No se puede instalar Npgsql automáticamente." -ForegroundColor Red
            Write-Host "Por favor instale Npgsql manualmente o ejecute el script SQL directamente." -ForegroundColor Yellow
            exit 1
        }
    }

    # Crear conexión
    $connection = New-Object Npgsql.NpgsqlConnection($ConnectionString)
    $connection.Open()
    
    Write-Host "✅ Conexión establecida correctamente" -ForegroundColor Green

    # Leer y ejecutar el script SQL
    $sqlFile = Join-Path $PSScriptRoot "insert_parametros_tarifas.sql"
    
    if (-not (Test-Path $sqlFile)) {
        Write-Host "❌ Error: No se encontró el archivo $sqlFile" -ForegroundColor Red
        exit 1
    }

    $sqlContent = Get-Content $sqlFile -Raw
    
    # Dividir por comandos individuales (cada INSERT)
    $commands = $sqlContent -split "(?=INSERT INTO)" | Where-Object { $_.Trim() -ne "" }
    
    foreach ($cmd in $commands) {
        if ($cmd.Trim() -like "INSERT INTO*" -or $cmd.Trim() -like "SELECT*") {
            try {
                $command = New-Object Npgsql.NpgsqlCommand($cmd.Trim(), $connection)
                
                if ($cmd.Trim().StartsWith("SELECT")) {
                    Write-Host ""
                    Write-Host "📊 Verificando parámetros insertados:" -ForegroundColor Cyan
                    $reader = $command.ExecuteReader()
                    
                    $format = "{0,-30} {1,-15} {2,-50} {3,-6}"
                    Write-Host ($format -f "CLAVE", "VALOR", "DESCRIPCIÓN", "ACTIVO") -ForegroundColor White
                    Write-Host ($format -f "-----", "-----", "-----------", "------") -ForegroundColor Gray
                    
                    while ($reader.Read()) {
                        $clave = $reader["clave"]
                        $valor = $reader["valor"] 
                        $descripcion = $reader["descripcion"]
                        $activo = $reader["activo"]
                        
                        Write-Host ($format -f $clave, "`$$valor", $descripcion, $activo) -ForegroundColor Green
                    }
                    $reader.Close()
                } else {
                    $result = $command.ExecuteNonQuery()
                    Write-Host "✅ Comando ejecutado: $result fila(s) afectada(s)" -ForegroundColor Green
                }
            }
            catch {
                Write-Host "❌ Error ejecutando comando: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }

    Write-Host ""
    Write-Host "🎉 ¡Parámetros de tarifas insertados correctamente!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📝 Los siguientes parámetros están ahora configurables:" -ForegroundColor Cyan
    Write-Host "   • TARIFA_EMI_AOCR: Emisión AOCR (`$3,300.00)" -ForegroundColor White
    Write-Host "   • TARIFA_REN_AOCR: Renovación AOCR (`$3,300.00)" -ForegroundColor White  
    Write-Host "   • TARIFA_MOD_AOCR_INC: Modificación con inclusión (`$1,600.00)" -ForegroundColor White
    Write-Host "   • TARIFA_MOD_AOCR_SIN_INC: Modificación sin inclusión (`$80.00)" -ForegroundColor White
    Write-Host "   • TARIFA_INSPECCION_EXT: Inspección externa (`$500.00)" -ForegroundColor White
    Write-Host "   • TARIFA_VIATICOS_INSPECTOR: Viáticos diarios (`$80.00)" -ForegroundColor White
    Write-Host "   • PORCENTAJE_ADMIN_VIATICOS: % Gastos administrativos (8%)" -ForegroundColor White
    Write-Host ""
    Write-Host "💡 Ahora puede modificar estos valores desde la base de datos sin cambiar código." -ForegroundColor Yellow

}
catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    if ($connection -and $connection.State -eq 'Open') {
        $connection.Close()
        Write-Host "🔌 Conexión cerrada" -ForegroundColor Gray
    }
}