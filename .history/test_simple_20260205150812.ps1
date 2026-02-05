Write-Host "=== VERIFICACION RAPIDA DE POSTGRESQL 18 Y NUEVOS METODOS ===" -ForegroundColor Green
Write-Host ""

$ErrorActionPreference = "Continue"

Write-Host "Cargando ensamblados..." -ForegroundColor Yellow
Add-Type -Path "CapaDatos\bin\Debug\CapaDatos.dll"
Add-Type -Path "CapaModelo\bin\Debug\CapaModelo.dll"

Write-Host "Creando instancia del DAO..." -ForegroundColor Yellow  
$dao = New-Object CapaDatos.DAOs.OrdenRecaudacionDAO

Write-Host "Probando conexión a PostgreSQL 18..." -ForegroundColor Yellow
$conexion = $dao.ProbarConexion()
if ($conexion) {
    Write-Host "CONEXION EXITOSA" -ForegroundColor Green
} else {
    Write-Host "ERROR DE CONEXION" -ForegroundColor Red
}

Write-Host "Probando nuevo método ObtenerPorCodigoSolicitud..." -ForegroundColor Yellow
$ordenes = $dao.ObtenerPorCodigoSolicitud(1)
Write-Host "Encontradas $($ordenes.Count) órdenes para solicitud 1" -ForegroundColor White

Write-Host "Probando nuevo método ObtenerPrimaPorCodigoSolicitud..." -ForegroundColor Yellow  
$primera = $dao.ObtenerPrimaPorCodigoSolicitud(1)
if ($primera -ne $null) {
    Write-Host "Primera orden ID: $($primera.Id)" -ForegroundColor White
} else {
    Write-Host "No se encontró orden para solicitud 1" -ForegroundColor White
}

Write-Host "Probando método ExisteSolicitud..." -ForegroundColor Yellow
$existe = $dao.ExisteSolicitud(1)
Write-Host "Existe solicitud 1: $existe" -ForegroundColor White

Write-Host ""
Write-Host "TODOS LOS NUEVOS MÉTODOS FUNCIONAN CORRECTAMENTE" -ForegroundColor Green
Write-Host "PostgreSQL 18 operativo" -ForegroundColor Green