# Script PowerShell para verificar drivers ODBC instalados
# Especialmente para diagnosticar "IBM i Access ODBC Driver"

Write-Host "=== VERIFICACION DE DRIVERS ODBC ===" -ForegroundColor Green
Write-Host ""

try {
    # Verificar en registro de Windows
    $odbcKey = "HKLM:\SOFTWARE\ODBC\ODBCINST.INI\ODBC Drivers"
    
    if (Test-Path $odbcKey) {
        Write-Host "✅ Accediendo al registro ODBC..." -ForegroundColor Green
        
        $drivers = Get-ItemProperty -Path $odbcKey
        $installedDrivers = @()
        
        # Obtener todos los drivers instalados
        $drivers.PSObject.Properties | ForEach-Object {
            if ($_.Name -ne "PSPath" -and $_.Name -ne "PSParentPath" -and $_.Name -ne "PSChildName" -and $_.Name -ne "PSDrive" -and $_.Name -ne "PSProvider") {
                if ($_.Value -eq "Installed") {
                    $installedDrivers += $_.Name
                }
            }
        }
        
        Write-Host "Total drivers ODBC encontrados: $($installedDrivers.Count)" -ForegroundColor Yellow
        Write-Host ""
        
        # Buscar drivers IBM específicamente
        $ibmDrivers = $installedDrivers | Where-Object { $_ -match "IBM" }
        
        if ($ibmDrivers.Count -gt 0) {
            Write-Host "🎯 DRIVERS IBM ENCONTRADOS:" -ForegroundColor Green
            $ibmDrivers | ForEach-Object { Write-Host "  ✅ $_" -ForegroundColor Green }
            Write-Host ""
        } else {
            Write-Host "❌ NO SE ENCONTRARON DRIVERS IBM" -ForegroundColor Red
            Write-Host ""
        }
        
        # Buscar el driver específico
        $as400Driver = $installedDrivers | Where-Object { $_ -match "IBM.*Access" -or $_ -match "AS.?400" -or $_ -match "IBM.*i.*Access" }
        
        if ($as400Driver.Count -gt 0) {
            Write-Host "🎯 DRIVERS AS400/IBM i ACCESS ENCONTRADOS:" -ForegroundColor Green
            $as400Driver | ForEach-Object { Write-Host "  ✅ $_" -ForegroundColor Green }
        } else {
            Write-Host "❌ NO SE ENCONTRÓ 'IBM i Access ODBC Driver'" -ForegroundColor Red
            Write-Host ""
            Write-Host "Drivers disponibles:" -ForegroundColor Yellow
            $installedDrivers | Sort-Object | ForEach-Object { Write-Host "  • $_" -ForegroundColor Gray }
        }
        
    } else {
        Write-Host "❌ No se puede acceder al registro ODBC" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "=== VERIFICACION ADICIONAL ===" -ForegroundColor Cyan
    
    # Verificar en registro 32-bit también (para sistemas 64-bit)
    $odbc32Key = "HKLM:\SOFTWARE\Wow6432Node\ODBC\ODBCINST.INI\ODBC Drivers"
    if (Test-Path $odbc32Key) {
        Write-Host "✅ Verificando drivers ODBC 32-bit..." -ForegroundColor Green
        
        $drivers32 = Get-ItemProperty -Path $odbc32Key
        $installed32Drivers = @()
        
        $drivers32.PSObject.Properties | ForEach-Object {
            if ($_.Name -notlike "PS*" -and $_.Value -eq "Installed") {
                $installed32Drivers += $_.Name
            }
        }
        
        $ibm32Drivers = $installed32Drivers | Where-Object { $_ -match "IBM" }
        if ($ibm32Drivers.Count -gt 0) {
            Write-Host "🎯 DRIVERS IBM 32-BIT ENCONTRADOS:" -ForegroundColor Green
            $ibm32Drivers | ForEach-Object { Write-Host "  ✅ $_" -ForegroundColor Green }
        } else {
            Write-Host "❌ No hay drivers IBM en 32-bit" -ForegroundColor Yellow
        }
    }
    
} catch {
    Write-Host "❌ Error verificando drivers: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== COMANDOS UTILES ===" -ForegroundColor Cyan
Write-Host "Para instalar IBM i Access Client Solutions:"
Write-Host "1. Descargar desde: https://www.ibm.com/support/pages/ibm-i-access-client-solutions"
Write-Host "2. Ejecutar instalador y seleccionar 'ODBC Driver'"
Write-Host "3. Reiniciar el servicio web después de la instalación"
Write-Host ""
Write-Host "Para verificar manualmente:"
Write-Host "• Panel de Control > Herramientas Administrativas > Orígenes de datos ODBC (64-bit)"
Write-Host "• Panel de Control > Herramientas Administrativas > Orígenes de datos ODBC (32-bit)"