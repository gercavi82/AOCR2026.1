# Script para insertar parámetros de bancos configurables
# Elimina los valores "quemados" en BancoP9DAO reemplazándolos por parámetros

param(
    [string]$ConnectionString = $null,
    [string]$Server = "localhost",
    [string]$Database = "aocr_db", 
    [string]$Username = "postgres",
    [string]$Password = "",
    [switch]$Help
)

if ($Help) {
    Write-Host "Uso: .\insertar_parametros_bancos.ps1 [parámetros]"
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
    Write-Host "  .\insertar_parametros_bancos.ps1 -Password 'mi_password'"
    Write-Host "  .\insertar_parametros_bancos.ps1 -Server 'servidor' -Database 'bd' -Username 'user' -Password 'pass'"
    exit 0
}

# Construir cadena de conexión si no se proporcionó
if ([string]::IsNullOrEmpty($ConnectionString)) {
    $ConnectionString = "Host=$Server;Database=$Database;Username=$Username"
    if (-not [string]::IsNullOrEmpty($Password)) {
        $ConnectionString += ";Password=$Password"
    }
}

Write-Host "🏦 Insertando parámetros de bancos configurables..." -ForegroundColor Cyan

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
    $sqlFile = Join-Path $PSScriptRoot "insert_parametros_bancos.sql"
    
    if (-not (Test-Path $sqlFile)) {
        Write-Host "❌ Error: No se encontró el archivo $sqlFile" -ForegroundColor Red
        exit 1
    }

    $sqlContent = Get-Content $sqlFile -Raw
    
    # Dividir por comandos individuales (cada INSERT)
    $commands = $sqlContent -split "(?=INSERT INTO)" | Where-Object { $_.Trim() -ne "" }
    
    $insertedCount = 0
    
    foreach ($cmd in $commands) {
        if ($cmd.Trim() -like "INSERT INTO*" -or $cmd.Trim() -like "SELECT*") {
            try {
                $command = New-Object Npgsql.NpgsqlCommand($cmd.Trim(), $connection)
                
                if ($cmd.Trim().StartsWith("SELECT")) {
                    Write-Host ""
                    Write-Host "📊 Verificando parámetros de bancos insertados:" -ForegroundColor Cyan
                    $reader = $command.ExecuteReader()
                    
                    $format = "{0,-15} {1,-35} {2,-30} {3,-6}"
                    Write-Host ($format -f "CODIGO", "BANCO", "DESCRIPCIÓN", "ACTIVO") -ForegroundColor White
                    Write-Host ($format -f "------", "-----", "-----------", "------") -ForegroundColor Gray
                    
                    while ($reader.Read()) {
                        $clave = $reader["clave"].ToString().Replace("BANCO_", "")
                        $valor = $reader["valor"].ToString().Split('|')[0] # Solo el nombre del banco
                        $descripcion = $reader["descripcion"]
                        $activo = $reader["activo"]
                        
                        Write-Host ($format -f $clave, $valor, $descripcion, $activo) -ForegroundColor Green
                    }
                    $reader.Close()
                } else {
                    $result = $command.ExecuteNonQuery()
                    if ($result > 0) {
                        $insertedCount++
                        Write-Host "✅ Banco insertado correctamente" -ForegroundColor Green
                    }
                }
            }
            catch {
                Write-Host "❌ Error ejecutando comando: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }

    Write-Host ""
    Write-Host "🎉 ¡Parámetros de bancos insertados correctamente!" -ForegroundColor Green
    Write-Host "📈 Total de bancos configurados: $insertedCount" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "💡 Beneficios de la solución:" -ForegroundColor Yellow
    Write-Host "   • ❌ Eliminados valores hardcodeados en BancoP9DAO.cs" -ForegroundColor White
    Write-Host "   • ✅ Bancos configurables desde base de datos" -ForegroundColor White  
    Write-Host "   • ⚡ Agregar/modificar bancos sin cambiar código" -ForegroundColor White
    Write-Host "   • 🔧 Administración centralizada de instituciones financieras" -ForegroundColor White
    Write-Host ""
    Write-Host "📝 Para agregar un nuevo banco:" -ForegroundColor Cyan
    Write-Host "   INSERT INTO parametros (clave, valor, descripcion, activo)" -ForegroundColor White
    Write-Host "   VALUES ('BANCO_XXX', 'NOMBRE_BANCO|SIGLAS|Tipo', 'Descripción', true);" -ForegroundColor White

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