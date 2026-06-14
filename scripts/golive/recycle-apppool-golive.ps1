# T-0: reciclar App Pool post publicación
param(
    [Parameter(Mandatory = $true)]
    [string]$SiteName
)
& (Join-Path $PSScriptRoot "..\recycle-iis-fase0.ps1") -SiteName $SiteName
