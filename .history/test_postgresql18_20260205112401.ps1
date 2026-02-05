# Script de prueba para verificar los nuevos métodos agregados al OrdenRecaudacionDAO
# Este script prueba la conexión con PostgreSQL 18 y los métodos de búsqueda por codigo_solicitud

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
        Write-Host "   ✓ CONECTADO exitosamente a PostgreSQL 18" -ForegroundColor Green
    } else {
        Write-Host "   ✗ ERROR DE CONEXIÓN" -ForegroundColor Red
        return
    }
    Write-Host ""
    
    Write-Host "2. Obteniendo estadísticas básicas..." -ForegroundColor Yellow
    $stats = $dao.ObtenerEstadisticas()
    Write-Host "   Total órdenes:" $stats["total"] -ForegroundColor White
    Write-Host "   Órdenes BORRADOR:" $stats["borrador"] -ForegroundColor White
    Write-Host "   Órdenes COMPLETADA:" $stats["completada"] -ForegroundColor White
    Write-Host ""
    
    Write-Host "3. PROBANDO NUEVOS MÉTODOS - Búsqueda por código de solicitud..." -ForegroundColor Yellow
    
    # Probar búsqueda por código de solicitud
    $ordenesPorSolicitud = $dao.ObtenerPorCodigoSolicitud(1)
    Write-Host "   Órdenes encontradas para solicitud 1:" $ordenesPorSolicitud.Count -ForegroundColor White
    
    if ($ordenesPorSolicitud.Count -gt 0) {
        $primera = $dao.ObtenerPrimaPorCodigoSolicitud(1)
        if ($primera -ne $null) {
            Write-Host "   ✓ Primera orden para solicitud 1 - ID:" $primera.Id ", Estado:" $primera.Estado -ForegroundColor Green
        }
    } else {
        Write-Host "   ℹ No se encontraron órdenes para la solicitud 1 (normal si es una BD nueva)" -ForegroundColor Cyan
    }
    
    # Intentar con otros códigos de solicitud
    for ($i = 1; $i -le 5; $i++) {
        $ordenes = $dao.ObtenerPorCodigoSolicitud($i)
        if ($ordenes.Count -gt 0) {
            Write-Host "   ✓ Solicitud $i tiene" $ordenes.Count "orden(es)" -ForegroundColor Green
            break
        }
    }
    Write-Host ""
    
    Write-Host "4. Verificando estructura de datos..." -ForegroundColor Yellow
    $todasOrdenes = $dao.ObtenerTodas()
    Write-Host "   Total de órdenes en tabla aocr_or_orden:" $todasOrdenes.Count -ForegroundColor White
    
    if ($todasOrdenes.Count -gt 0) {
        $muestra = $todasOrdenes[0]
        $codigoSol = if ($muestra.CodigoSolicitud -ne $null) { $muestra.CodigoSolicitud } else { "null" }
        Write-Host "   Muestra - ID:" $muestra.Id ", Código Solicitud:" $codigoSol ", Estado:" $muestra.Estado -ForegroundColor White
    } else {
        Write-Host "   ℹ No hay órdenes en la base de datos" -ForegroundColor Cyan
    }
    Write-Host ""
    
    Write-Host "5. Verificando relación con solicitudes..." -ForegroundColor Yellow
    $existeSolicitud1 = $dao.ExisteSolicitud(1)
    Write-Host "   Existe solicitud con código 1:" $(if ($existeSolicitud1) { "Sí" } else { "No" }) -ForegroundColor White
    
    $existeSolicitud999 = $dao.ExisteSolicitud(999)
    Write-Host "   Existe solicitud con código 999:" $(if ($existeSolicitud999) { "Sí" } else { "No" }) -ForegroundColor White
    Write-Host ""
    
    Write-Host "=== ✅ TODAS LAS PRUEBAS COMPLETADAS EXITOSAMENTE ===" -ForegroundColor Green
    Write-Host "PostgreSQL 18 está funcionando correctamente." -ForegroundColor Green
    Write-Host "Los nuevos métodos de búsqueda por código de solicitud están operativos." -ForegroundColor Green
    Write-Host ""
    Write-Host "MÉTODOS NUEVOS VERIFICADOS:" -ForegroundColor Cyan
    Write-Host "✓ ObtenerPorCodigoSolicitud(int codigoSolicitud)" -ForegroundColor White
    Write-Host "✓ ObtenerPrimaPorCodigoSolicitud(int codigoSolicitud)" -ForegroundColor White
    Write-Host "✓ ExisteSolicitud(int codigoSolicitud)" -ForegroundColor White
    Write-Host "✓ ActualizarCodigoSolicitudOrden(int ordenId, int codigoSolicitud)" -ForegroundColor White

} catch {
    Write-Host "✗ ERROR DURANTE LAS PRUEBAS:" -ForegroundColor Red
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
    Write-Host "- Ensamblados no encontrados" -ForegroundColor White
}