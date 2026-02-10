# ========================================
# Script: Verificar conexión AS/400
# ========================================

Write-Host "`n=== DIAGNÓSTICO CONEXIÓN AS/400 ===" -ForegroundColor Cyan

# 1. Verificar driver ODBC instalado
Write-Host "`n[1/5] Verificando driver IBM i Access ODBC..." -ForegroundColor Yellow

try {
    $drivers = Get-OdbcDriver | Where-Object { $_.Name -like "*IBM*" }
    
    if ($drivers) {
        Write-Host "✅ Driver encontrado:" -ForegroundColor Green
        $drivers | Format-Table Name, Platform, Description -AutoSize
    } else {
        Write-Host "❌ Driver IBM i Access ODBC NO está instalado" -ForegroundColor Red
        Write-Host "   Descargue e instale desde:" -ForegroundColor Yellow
        Write-Host "   https://www.ibm.com/support/pages/ibm-i-access-client-solutions`n" -ForegroundColor Cyan
        Write-Host "   Después de instalar, ejecute este script nuevamente.`n" -ForegroundColor Yellow
        Read-Host "Presione Enter para salir"
        exit
    }
} catch {
    Write-Host "❌ Error verificando drivers: $($_.Exception.Message)" -ForegroundColor Red
    exit
}

# 2. Parámetros de conexión
Write-Host "`n[2/5] Verificando parámetros de conexión..." -ForegroundColor Yellow

$server = "172.20.16.14"
$library = "DGACSYS"
$user = "DGAC"
$password = "DGAC2024"

Write-Host "  Servidor: $server" -ForegroundColor Gray
Write-Host "  Biblioteca: $library" -ForegroundColor Gray
Write-Host "  Usuario: $user" -ForegroundColor Gray

# 3. Probar conexión básica
Write-Host "`n[3/5] Probando conexión al AS/400..." -ForegroundColor Yellow

$connectionString = "Driver={IBM i Access ODBC Driver};System=$server;DefaultCollection=$library;Uid=$user;Pwd=$password;"

try {
    $connection = New-Object System.Data.Odbc.OdbcConnection($connectionString)
    $connection.Open()
    Write-Host "✅ Conexión exitosa al AS/400" -ForegroundColor Green
    $connection.Close()
} catch {
    Write-Host "❌ Error de conexión: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "`nPosibles causas:" -ForegroundColor Yellow
    Write-Host "  1. El servidor AS/400 no está accesible (verificar red/VPN)" -ForegroundColor Gray
    Write-Host "  2. Credenciales incorrectas" -ForegroundColor Gray
    Write-Host "  3. Firewall bloqueando puerto (típicamente 8471)" -ForegroundColor Gray
    Write-Host "  4. Biblioteca no existe o sin permisos`n" -ForegroundColor Gray
    
    Read-Host "Presione Enter para continuar con diagnóstico extendido"
}

# 4. Probar consulta a CIAARC
Write-Host "`n[4/5] Probando consulta a tabla CIAARC..." -ForegroundColor Yellow

try {
    $connection = New-Object System.Data.Odbc.OdbcConnection($connectionString)
    $connection.Open()
    
    $command = $connection.CreateCommand()
    $command.CommandText = "SELECT COUNT(*) as Total FROM CIAARC WHERE CIAEST='AC'"
    
    $reader = $command.ExecuteReader()
    
    if ($reader.Read()) {
        $total = $reader["Total"]
        Write-Host "✅ Tabla CIAARC accesible" -ForegroundColor Green
        Write-Host "✅ Empresas activas encontradas: $total" -ForegroundColor Green
        
        if ($total -eq 0) {
            Write-Host "⚠️  ADVERTENCIA: No hay empresas con estado 'AC'" -ForegroundColor Yellow
        }
    }
    
    $reader.Close()
    $connection.Close()
    
} catch {
    Write-Host "❌ Error consultando CIAARC: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.Exception.Message -like "*CIAARC*not found*") {
        Write-Host "`n⚠️  La tabla CIAARC no existe en biblioteca $library" -ForegroundColor Yellow
        Write-Host "   Verificar nombre correcto de tabla y biblioteca`n" -ForegroundColor Gray
    }
}

# 5. Probar endpoint de aplicación
Write-Host "`n[5/5] Probando endpoint /Empresa/ObtenerEmpresas..." -ForegroundColor Yellow

$appUrl = "https://localhost:44333/Empresa/ObtenerEmpresas"

try {
    Write-Host "  URL: $appUrl" -ForegroundColor Gray
    Write-Host "  (Asegúrese de que IIS Express esté ejecutándose)" -ForegroundColor Gray
    
    # Ignorar errores de certificado SSL para localhost
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
    
    $response = Invoke-WebRequest -Uri $appUrl -Method Get -UseBasicParsing -TimeoutSec 10
    
    if ($response.StatusCode -eq 200) {
        Write-Host "✅ Endpoint responde correctamente" -ForegroundColor Green
        
        $data = $response.Content | ConvertFrom-Json
        Write-Host "✅ Empresas devueltas: $($data.Count)" -ForegroundColor Green
        
        if ($data.Count -gt 0) {
            Write-Host "`n📋 Primeras 5 empresas:" -ForegroundColor Cyan
            $data | Select-Object -First 5 | ForEach-Object {
                Write-Host "   [$($_.CodigoOaci)] $($_.Nombre)" -ForegroundColor Gray
            }
        } else {
            Write-Host "⚠️  El endpoint devuelve array vacío" -ForegroundColor Yellow
        }
    }
    
} catch {
    if ($_.Exception.Message -like "*No se puede conectar*") {
        Write-Host "⚠️  IIS Express no está ejecutándose" -ForegroundColor Yellow
        Write-Host "   Presione F5 en Visual Studio para iniciar la aplicación`n" -ForegroundColor Gray
    } elseif ($_.Exception.Message -like "*500*") {
        Write-Host "❌ Error 500 en servidor - revisar Output en Visual Studio" -ForegroundColor Red
        Write-Host "   View → Output → Seleccionar 'Debug' en dropdown`n" -ForegroundColor Gray
    } else {
        Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Resumen final
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "DIAGNÓSTICO COMPLETADO" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "Próximos pasos:" -ForegroundColor Yellow
Write-Host "1. Si los tests pasaron, reiniciar IIS Express (Shift+F5, luego F5)" -ForegroundColor Gray
Write-Host "2. Abrir navegador en https://localhost:44333" -ForegroundColor Gray
Write-Host "3. Abrir modal de registro" -ForegroundColor Gray
Write-Host "4. Presionar F12 → Console para ver logs" -ForegroundColor Gray
Write-Host "5. Verificar que dropdown de empresas se llene`n" -ForegroundColor Gray

Read-Host "Presione Enter para salir"
