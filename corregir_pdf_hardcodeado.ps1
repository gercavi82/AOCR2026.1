#!/usr/bin/env pwsh
# Script para corregir valores hardcodeados en generación de PDFs de órdenes de recaudación

Write-Host "CORRECCIÓN DE VALORES HARDCODEADOS EN PDFs DE ÓRDENES" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Gray
Write-Host ""
Write-Host "RESUMEN DE CAMBIOS REALIZADOS:" -ForegroundColor Green
Write-Host ""

# 1. Mostrar cambios en ParametroDAO
Write-Host "1. ParametroDAO.cs - Método ObtenerParametrosCalculoOrden() agregado" -ForegroundColor Green
Write-Host "   • Obtiene parámetros dinámicos desde la base de datos"
Write-Host "   • CALCULO_VALOR_POR_ESTACION, CALCULO_VALOR_POR_DIA_VIATICO, CALCULO_PORCENTAJE_GASTOS_ADMIN"

# 2. Mostrar cambios en OrdenRecaudacionPDFModel
Write-Host ""
Write-Host "2. OrdenRecaudacionPDFModel.cs - CalcularTotales() corregido" -ForegroundColor Green
Write-Host "   • Eliminados valores hardcodeados: 500m, 80m, 0.08m"
Write-Host "   • Implementada carga dinámica de parámetros con fallback"

# 3. Mostrar cambios en OrdenRecaudacionPdfDto
Write-Host ""
Write-Host "3. OrdenRecaudacionPdfDto.cs - CalcularTotales() corregido" -ForegroundColor Green
Write-Host "   • Eliminado porcentaje hardcodeado: 0.08m (8%)"
Write-Host "   • Implementada carga dinámica con manejo de errores"

Write-Host ""
Write-Host "CONFIGURACIÓN DE BASE DE DATOS:" -ForegroundColor Yellow

# 4. Ejecutar script de base de datos si existe
$sqlScript = "insert_parametros_calculo_orden.sql"
if (Test-Path $sqlScript) {
    Write-Host "Script SQL encontrado: $sqlScript" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Para completar la configuración, ejecuta el script SQL:" -ForegroundColor White
    Write-Host "psql -U tu_usuario -d tu_base_datos -f $sqlScript" -ForegroundColor Gray
}
else {
    Write-Host "Script SQL no encontrado: $sqlScript" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "VERIFICACIÓN MANUAL NECESARIA:" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Verifica que los parámetros se insertaron en aocr_tbparametro:" -ForegroundColor White
Write-Host "   SELECT clave, valor, descripcion FROM aocr_tbparametro WHERE clave LIKE 'CALCULO_%';" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Compila el proyecto para verificar que no hay errores:" -ForegroundColor White
Write-Host "   Build -> Rebuild Solution en Visual Studio" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Prueba generar un PDF de orden de recaudación:" -ForegroundColor White
Write-Host "   • Ve a una orden existente y haz clic en 'Descargar PDF'" -ForegroundColor Gray
Write-Host "   • Verifica que los montos ahora son dinámicos" -ForegroundColor Gray
Write-Host ""
Write-Host "4. Si necesitas cambiar los valores, actualiza en la base de datos:" -ForegroundColor White
Write-Host "   UPDATE aocr_tbparametro SET valor = '600.00' WHERE clave = 'CALCULO_VALOR_POR_ESTACION';" -ForegroundColor Gray

Write-Host ""
Write-Host "PROBLEMA RESUELTO:" -ForegroundColor Green
Write-Host "   ANTES: PDF con valores hardcodeados ($500, $80, 8%)" -ForegroundColor Red
Write-Host "   AHORA: PDF con valores configurables desde base de datos" -ForegroundColor Green

Write-Host ""
Write-Host "Los archivos modificados están listos. Recuerda hacer commit de los cambios." -ForegroundColor Blue
Write-Host "=====================================================" -ForegroundColor Gray