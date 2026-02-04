# Script simple para verificar drivers ODBC
Write-Host "=== VERIFICACION DE DRIVERS ODBC ===" -ForegroundColor Green

try {
    $odbcKey = "HKLM:\SOFTWARE\ODBC\ODBCINST.INI\ODBC Drivers"
    
    if (Test-Path $odbcKey) {
        Write-Host "Accediendo al registro ODBC..." -ForegroundColor Green
        
        $drivers = Get-ItemProperty -Path $odbcKey
        $installedDrivers = @()
        
        $drivers.PSObject.Properties | ForEach-Object {
            if ($_.Name -notlike "PS*" -and $_.Value -eq "Installed") {
                $installedDrivers += $_.Name
            }
        }
        
        Write-Host "Total drivers encontrados: $($installedDrivers.Count)" -ForegroundColor Yellow
        
        # Buscar drivers IBM
        $ibmDrivers = $installedDrivers | Where-Object { $_ -match "IBM" }
        
        if ($ibmDrivers.Count -gt 0) {
            Write-Host "DRIVERS IBM ENCONTRADOS:" -ForegroundColor Green
            $ibmDrivers | ForEach-Object { Write-Host "  - $_" -ForegroundColor Green }
        } else {
            Write-Host "NO SE ENCONTRARON DRIVERS IBM" -ForegroundColor Red
        }
        
        Write-Host ""
        Write-Host "TODOS LOS DRIVERS:" -ForegroundColor Cyan
        $installedDrivers | Sort-Object | ForEach-Object { Write-Host "  - $_" }
        
    } else {
        Write-Host "No se puede acceder al registro ODBC" -ForegroundColor Red
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}