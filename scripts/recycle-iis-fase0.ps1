# Recicla el App Pool del sitio AOCR en publicacion1 (ejecutar como admin en el servidor IIS).
param(
    [string]$SiteName = "",
    [string]$AppPoolName = ""
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command appcmd.exe -ErrorAction SilentlyContinue)) {
    $appcmd = Join-Path $env:windir "system32\inetsrv\appcmd.exe"
} else {
    $appcmd = "appcmd.exe"
}

if (-not (Test-Path $appcmd)) {
    Write-Error "IIS/appcmd no disponible. Ejecute este script en el servidor donde corre publicacion1."
}

if ([string]::IsNullOrWhiteSpace($SiteName)) {
    Write-Host "Sitios IIS:"
    & $appcmd list sites /text:name,physicalPath
    Write-Host ""
    Write-Host "Uso: .\recycle-iis-fase0.ps1 -SiteName 'NombreSitio'"
    Write-Host "  o: .\recycle-iis-fase0.ps1 -AppPoolName 'NombreAppPool'"
    exit 0
}

if ([string]::IsNullOrWhiteSpace($AppPoolName)) {
    $AppPoolName = (& $appcmd list site /site.name:$SiteName /text:applicationPool).Trim()
}

Write-Host "Reciclando App Pool: $AppPoolName (sitio: $SiteName)"
& $appcmd recycle apppool /apppool.name:$AppPoolName
Write-Host "OK — App Pool reciclado $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
