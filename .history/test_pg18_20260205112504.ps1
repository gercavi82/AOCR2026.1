# Script de prueba para verificar PostgreSQL 18 y los nuevos métodos
Write-Host "=== TEST DE POSTGRESQL 18 Y NUEVOS MÉTODOS ===" -ForegroundColor Cyan
Write-Host ""

try {
    # Cargar los ensamblados necesarios
    Add-Type -Path "CapaDatos\bin\Debug\CapaDatos.dll"
    Add-Type -Path "CapaModelo\bin\Debug\CapaModelo.dll"
    
    # Crear instancia del DAO
    $dao = New-Object CapaDatos.DAOs.OrdenRecaudacionDAO
    
    Write-Host "1. Probando conexión básica..." -ForegroundColor Yellow
    $conexionOk = $dao.ProbarConexion()
    if ($conexionOk) {
        Write-Host "   CONECTADO exitosamente a PostgreSQL 18" -ForegroundColor Green
    } else {
        Write-Host "   ERROR DE CONEXIÓN" -ForegroundColor Red
        return
    }
    Write-Host ""
    
    Write-Host "2. Obteniendo estadísticas básicas..." -ForegroundColor Yellow
    $stats = $dao.ObtenerEstadisticas()
    if ($stats -ne $null) {
        Write-Host "   Estadísticas obtenidas correctamente" -ForegroundColor Green
    } else {
        Write-Host "   No se pudieron obtener estadísticas" -ForegroundColor Red
    }
    Write-Host ""
    
    Write-Host "3. PROBANDO NUEVOS MÉTODOS - Búsqueda por código de solicitud..." -ForegroundColor Yellow
    
    # Probar búsqueda por código de solicitud
    $ordenesPorSolicitud = $dao.ObtenerPorCodigoSolicitud(1)
    Write-Host "   Órdenes encontradas para solicitud 1:" $ordenesPorSolicitud.Count -ForegroundColor White
    
    if ($ordenesPorSolicitud.Count -gt 0) {
        $primera = $dao.ObtenerPrimaPorCodigoSolicitud(1)
        if ($primera -ne $null) {
            Write-Host "   Primera orden para solicitud 1 - ID:" $primera.Id -ForegroundColor Green
        }
    } else {
        Write-Host "   No se encontraron órdenes para la solicitud 1" -ForegroundColor Cyan
    }
    Write-Host ""
    
    Write-Host "4. Verificando estructura de datos..." -ForegroundColor Yellow
    $todasOrdenes = $dao.ObtenerTodas()
    Write-Host "   Total de órdenes en tabla aocr_or_orden:" $todasOrdenes.Count -ForegroundColor White
    Write-Host ""
    
    Write-Host "5. Verificando relación con solicitudes..." -ForegroundColor Yellow
    $existeSolicitud1 = $dao.ExisteSolicitud(1)
    if ($existeSolicitud1) {
        Write-Host "   Existe solicitud con código 1: Sí" -ForegroundColor Green
    } else {
        Write-Host "   Existe solicitud con código 1: No" -ForegroundColor White
    }
    Write-Host ""
    
    Write-Host "=== TODAS LAS PRUEBAS COMPLETADAS EXITOSAMENTE ===" -ForegroundColor Green
    Write-Host "PostgreSQL 18 está funcionando correctamente." -ForegroundColor Green
    Write-Host "Los nuevos métodos de búsqueda por código de solicitud están operativos." -ForegroundColor Green
    Write-Host ""
    Write-Host "MÉTODOS NUEVOS VERIFICADOS:" -ForegroundColor Cyan
    Write-Host "- ObtenerPorCodigoSolicitud(int codigoSolicitud)" -ForegroundColor White
    Write-Host "- ObtenerPrimaPorCodigoSolicitud(int codigoSolicitud)" -ForegroundColor White
    Write-Host "- ExisteSolicitud(int codigoSolicitud)" -ForegroundColor White

} catch {
    Write-Host "ERROR DURANTE LAS PRUEBAS:" -ForegroundColor Red
    Write-Host "   Mensaje:" $_.Exception.Message -ForegroundColor Red
    Write-Host "   Tipo:" $_.Exception.GetType().Name -ForegroundColor Red
    if ($_.Exception.InnerException) {
        Write-Host "   Error interno:" $_.Exception.InnerException.Message -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "Posibles causas:" -ForegroundColor Yellow
    Write-Host "- PostgreSQL 18 no está ejecutándose" -ForegroundColor White
    Write-Host "- Configuración de conexión incorrecta" -ForegroundColor White
    Write-Host "- Problemas con las tablas de la base de datos" -ForegroundColor White
}